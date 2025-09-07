using System.Security.Cryptography;
using System.Text;
using Audiarr.Api.Data;
using Audiarr.Api.Models;
using Audiarr.Api.Models.Entities;
using Audiarr.Api.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using TagLib;
using File = System.IO.File;

namespace Audiarr.Api.Services;

public class LibraryScanner : ILibraryScanner
{
    private readonly AudiarrContext _context;
    private readonly ILogger<LibraryScanner> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly HashSet<string> _audioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wav", ".wma", ".alac", ".ape", ".wv", ".mka"
    };
    private readonly string[] _artworkFileNames = { "cover.jpg", "folder.jpg", "album.jpg", "front.jpg", "cover.png", "folder.png", "album.png", "front.png" };

    public LibraryScanner(AudiarrContext context, ILogger<LibraryScanner> logger, IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _environment = environment;
    }

    public async Task<ScanResult> ScanAsync(string libraryPath, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = new ScanResult
        {
            StartTime = DateTime.UtcNow
        };

        if (!Directory.Exists(libraryPath))
        {
            result.ErrorMessages.Add($"Library path does not exist: {libraryPath}");
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        try
        {
            _logger.LogInformation("Starting library scan at: {Path}", libraryPath);

            // Get all audio files
            var audioFiles = Directory.GetFiles(libraryPath, "*.*", SearchOption.AllDirectories)
                .Where(IsAudioFile)
                .ToList();

            result.TotalFiles = audioFiles.Count;
            _logger.LogInformation("Found {Count} audio files to process", audioFiles.Count);

            foreach (var filePath in audioFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Scan cancelled by user");
                    break;
                }

                try
                {
                    var fileResult = await ScanFileAsync(filePath, cancellationToken);
                    result.ProcessedFiles++;
                    result.NewTracks += fileResult.NewTracks;
                    result.UpdatedTracks += fileResult.UpdatedTracks;

                    // Report progress
                    progress?.Report(new ScanProgress
                    {
                        ProcessedFiles = result.ProcessedFiles,
                        TotalFiles = result.TotalFiles,
                        CurrentFile = Path.GetFileName(filePath)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file: {Path}", filePath);
                    result.Errors++;
                    result.ErrorMessages.Add($"Error processing {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Library scan completed. Processed: {Processed}, New: {New}, Updated: {Updated}, Errors: {Errors}",
                result.ProcessedFiles, result.NewTracks, result.UpdatedTracks, result.Errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during library scan");
            result.ErrorMessages.Add($"Fatal error: {ex.Message}");
        }

        result.EndTime = DateTime.UtcNow;
        return result;
    }

    public async Task<ScanResult> ScanFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = new ScanResult
        {
            StartTime = DateTime.UtcNow
        };

        if (!File.Exists(filePath))
        {
            result.ErrorMessages.Add($"File does not exist: {filePath}");
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        try
        {
            // Calculate file hash for deduplication
            var fileHash = await CalculateFileHashAsync(filePath);
            
            // Check if track already exists
            var existingTrack = await _context.Tracks
                .Include(t => t.Album)
                .ThenInclude(a => a!.Artist)
                .Include(t => t.Artist)
                .FirstOrDefaultAsync(t => t.FileHash == fileHash, cancellationToken);

            using var file = TagLib.File.Create(filePath);
            var tag = file.Tag;
            var properties = file.Properties;

            // Get or create artist
            var artistName = !string.IsNullOrWhiteSpace(tag.FirstAlbumArtist) ? tag.FirstAlbumArtist :
                            !string.IsNullOrWhiteSpace(tag.FirstPerformer) ? tag.FirstPerformer : "Unknown Artist";
            
            var artist = await GetOrCreateArtistAsync(artistName, cancellationToken);

            // Get or create album
            var albumTitle = !string.IsNullOrWhiteSpace(tag.Album) ? tag.Album : "Unknown Album";
            var album = await GetOrCreateAlbumAsync(albumTitle, artist.Id, tag.Year, cancellationToken);

            // Extract cover art if available
            if (album.CoverArtPath == null)
            {
                // First try embedded artwork
                if (tag.Pictures.Length > 0)
                {
                    var coverArtPath = await SaveCoverArtFromPictureAsync(tag.Pictures[0], album.Id);
                    if (coverArtPath != null)
                    {
                        album.CoverArtPath = coverArtPath;
                        _context.Albums.Update(album);
                    }
                }
                // If no embedded artwork, look for folder artwork
                else
                {
                    var folderArtworkPath = await FindAndSaveFolderArtworkAsync(filePath, album.Id);
                    if (folderArtworkPath != null)
                    {
                        album.CoverArtPath = folderArtworkPath;
                        _context.Albums.Update(album);
                    }
                }
            }

            if (existingTrack != null)
            {
                // Update existing track
                existingTrack.Title = !string.IsNullOrWhiteSpace(tag.Title) ? tag.Title : Path.GetFileNameWithoutExtension(filePath);
                existingTrack.AlbumId = album.Id;
                existingTrack.ArtistId = artist.Id;
                existingTrack.TrackNumber = tag.Track > 0 ? (int)tag.Track : null;
                existingTrack.DiscNumber = tag.Disc > 0 ? (int)tag.Disc : null;
                existingTrack.DurationMs = (int)(properties.Duration.TotalMilliseconds);
                existingTrack.BitRate = properties.AudioBitrate;
                existingTrack.SampleRate = properties.AudioSampleRate;
                existingTrack.FilePath = filePath;
                existingTrack.FileSizeBytes = new FileInfo(filePath).Length;
                existingTrack.UpdatedAt = DateTime.UtcNow;

                _context.Tracks.Update(existingTrack);
                result.UpdatedTracks = 1;
                _logger.LogDebug("Updated track: {Title} by {Artist}", existingTrack.Title, artistName);
            }
            else
            {
                // Create new track
                var track = new Track
                {
                    Title = !string.IsNullOrWhiteSpace(tag.Title) ? tag.Title : Path.GetFileNameWithoutExtension(filePath),
                    AlbumId = album.Id,
                    ArtistId = artist.Id,
                    TrackNumber = tag.Track > 0 ? (int)tag.Track : null,
                    DiscNumber = tag.Disc > 0 ? (int)tag.Disc : null,
                    DurationMs = (int)(properties.Duration.TotalMilliseconds),
                    BitRate = properties.AudioBitrate,
                    SampleRate = properties.AudioSampleRate,
                    FilePath = filePath,
                    FileHash = fileHash,
                    FileSizeBytes = new FileInfo(filePath).Length,
                    Genre = tag.FirstGenre,
                    Year = tag.Year > 0 ? (int)tag.Year : null
                };

                await _context.Tracks.AddAsync(track, cancellationToken);
                result.NewTracks = 1;
                _logger.LogDebug("Added new track: {Title} by {Artist}", track.Title, artistName);
            }

            result.ProcessedFiles = 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning file: {Path}", filePath);
            result.Errors = 1;
            result.ErrorMessages.Add($"Error: {ex.Message}");
        }

        result.EndTime = DateTime.UtcNow;
        return result;
    }

    public bool IsAudioFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return _audioExtensions.Contains(extension);
    }

    private async Task<Artist> GetOrCreateArtistAsync(string name, CancellationToken cancellationToken)
    {
        var normalized = NormalizeString(name);
        var artist = await _context.Artists
            .FirstOrDefaultAsync(a => a.NameNormalized == normalized, cancellationToken);

        if (artist == null)
        {
            artist = new Artist
            {
                Name = name,
                SortName = GetSortName(name),
                NameNormalized = normalized
            };
            await _context.Artists.AddAsync(artist, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken); // Save immediately to prevent duplicates
            _logger.LogDebug("Created new artist: {Name}", name);
        }

        return artist;
    }

    private async Task<Album> GetOrCreateAlbumAsync(string title, string artistId, uint year, CancellationToken cancellationToken)
    {
        var normalized = NormalizeString(title);
        var album = await _context.Albums
            .FirstOrDefaultAsync(a => a.TitleNormalized == normalized && a.ArtistId == artistId, cancellationToken);

        if (album == null)
        {
            album = new Album
            {
                Title = title,
                TitleNormalized = normalized,
                ArtistId = artistId,
                ReleaseYear = year > 0 ? (int)year : null
            };
            await _context.Albums.AddAsync(album, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken); // Save immediately to prevent duplicates
            _logger.LogDebug("Created new album: {Title}", title);
        }

        return album;
    }

    private string GetArtworkDirectory()
    {
        // Use Data/artwork for development, /data/artwork for Docker
        var baseDir = _environment.IsDevelopment() 
            ? Path.Combine(Directory.GetCurrentDirectory(), "Data")
            : "/data";
        return Path.Combine(baseDir, "artwork");
    }

    private async Task<string?> SaveCoverArtFromPictureAsync(IPicture picture, string albumId)
    {
        try
        {
            var coverArtDir = GetArtworkDirectory();
            Directory.CreateDirectory(coverArtDir);

            var extension = picture.MimeType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => ".jpg"
            };

            var fileName = $"{albumId}{extension}";
            var filePath = Path.Combine(coverArtDir, fileName);

            await File.WriteAllBytesAsync(filePath, picture.Data.Data);
            _logger.LogDebug("Saved embedded cover art for album: {AlbumId}", albumId);

            return $"/artwork/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving embedded cover art for album: {AlbumId}", albumId);
            return null;
        }
    }

    private async Task<string?> FindAndSaveFolderArtworkAsync(string audioFilePath, string albumId)
    {
        try
        {
            var directory = Path.GetDirectoryName(audioFilePath);
            if (string.IsNullOrEmpty(directory))
                return null;

            // Look for common artwork file names in the same directory
            foreach (var artworkFileName in _artworkFileNames)
            {
                var artworkPath = Path.Combine(directory, artworkFileName);
                if (File.Exists(artworkPath))
                {
                    return await SaveCoverArtFromFileAsync(artworkPath, albumId);
                }
            }

            // Check for any jpg/png files in the directory
            var imageFiles = Directory.GetFiles(directory, "*.jpg")
                .Concat(Directory.GetFiles(directory, "*.jpeg"))
                .Concat(Directory.GetFiles(directory, "*.png"))
                .OrderBy(f => f) // Use first alphabetically
                .FirstOrDefault();

            if (imageFiles != null)
            {
                return await SaveCoverArtFromFileAsync(imageFiles, albumId);
            }

            // Check for Artwork subdirectory
            var artworkDir = Path.Combine(directory, "Artwork");
            if (Directory.Exists(artworkDir))
            {
                var artworkFile = Directory.GetFiles(artworkDir, "*.jpg")
                    .Concat(Directory.GetFiles(artworkDir, "*.jpeg"))
                    .Concat(Directory.GetFiles(artworkDir, "*.png"))
                    .Where(f => Path.GetFileName(f).ToLower().Contains("front") || 
                               Path.GetFileName(f).ToLower().Contains("cover"))
                    .FirstOrDefault();

                if (artworkFile == null)
                {
                    artworkFile = Directory.GetFiles(artworkDir, "*.jpg")
                        .Concat(Directory.GetFiles(artworkDir, "*.jpeg"))
                        .Concat(Directory.GetFiles(artworkDir, "*.png"))
                        .OrderBy(f => f)
                        .FirstOrDefault();
                }

                if (artworkFile != null)
                {
                    return await SaveCoverArtFromFileAsync(artworkFile, albumId);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding folder artwork for album: {AlbumId}", albumId);
            return null;
        }
    }

    private async Task<string?> SaveCoverArtFromFileAsync(string sourceFilePath, string albumId)
    {
        try
        {
            var coverArtDir = GetArtworkDirectory();
            Directory.CreateDirectory(coverArtDir);

            var extension = Path.GetExtension(sourceFilePath).ToLower();
            var fileName = $"{albumId}{extension}";
            var destPath = Path.Combine(coverArtDir, fileName);

            // Copy the file to our artwork directory
            File.Copy(sourceFilePath, destPath, overwrite: true);
            _logger.LogDebug("Saved folder cover art for album: {AlbumId} from {Source}", albumId, sourceFilePath);

            return $"/artwork/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving folder cover art for album: {AlbumId}", albumId);
            return null;
        }
    }

    private async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        
        // Read first 64KB and last 64KB for faster hashing of large files
        const int sampleSize = 65536;
        var buffer = new byte[sampleSize];
        
        // Read beginning
        var bytesRead = await stream.ReadAsync(buffer, 0, sampleSize);
        sha256.TransformBlock(buffer, 0, bytesRead, buffer, 0);
        
        // Read end if file is large enough
        if (stream.Length > sampleSize * 2)
        {
            stream.Seek(-sampleSize, SeekOrigin.End);
            bytesRead = await stream.ReadAsync(buffer, 0, sampleSize);
            sha256.TransformBlock(buffer, 0, bytesRead, buffer, 0);
        }
        
        // Include file size in hash
        var sizeBytes = BitConverter.GetBytes(stream.Length);
        sha256.TransformFinalBlock(sizeBytes, 0, sizeBytes.Length);
        
        return Convert.ToBase64String(sha256.Hash!);
    }

    private string NormalizeString(string input)
    {
        return input.ToLowerInvariant()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("_", "")
            .Replace(".", "")
            .Replace(",", "")
            .Replace("'", "")
            .Replace("\"", "");
    }

    private string GetSortName(string name)
    {
        var prefixes = new[] { "the ", "a ", "an " };
        var lowerName = name.ToLowerInvariant();
        
        foreach (var prefix in prefixes)
        {
            if (lowerName.StartsWith(prefix))
            {
                return name.Substring(prefix.Length) + ", " + name.Substring(0, prefix.Length - 1);
            }
        }
        
        return name;
    }
}