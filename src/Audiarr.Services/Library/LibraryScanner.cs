using System.Security.Cryptography;
using System.Text;
using Audiarr.Data.Context;
using Audiarr.Core.Entities;
using Audiarr.Core.Interfaces;
using Audiarr.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TagLib;
using File = System.IO.File;

namespace Audiarr.Services.Library;

public class LibraryScanner : ILibraryScanner
{
    private readonly AudiarrContext _context;
    private readonly ILogger<LibraryScanner> _logger;
    private readonly IHostEnvironment _environment;
    private readonly MultiValuedTagsOptions _multiValuedTagsOptions;
    private readonly HashSet<string> _audioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wav", ".wma", ".alac", ".ape", ".wv", ".mka"
    };
    private readonly string[] _artworkFileNames = { "cover.jpg", "folder.jpg", "album.jpg", "front.jpg", "cover.png", "folder.png", "album.png", "front.png" };

    public LibraryScanner(AudiarrContext context, ILogger<LibraryScanner> logger, IHostEnvironment environment, IOptions<MultiValuedTagsOptions> multiValuedTagsOptions)
    {
        _context = context;
        _logger = logger;
        _environment = environment;
        _multiValuedTagsOptions = multiValuedTagsOptions.Value;
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
                .Include(t => t.Album)
                    .ThenInclude(a => a!.AlbumArtists)
                .Include(t => t.Album)
                    .ThenInclude(a => a!.AlbumGenres)
                        .ThenInclude(ag => ag.Genre)
                .Include(t => t.Artist)
                .Include(t => t.TrackArtists)
                .Include(t => t.TrackGenres)
                    .ThenInclude(tg => tg.Genre)
                .FirstOrDefaultAsync(t => t.FileHash == fileHash, cancellationToken);

            using var file = TagLib.File.Create(filePath);
            var tag = file.Tag;
            var properties = file.Properties;

            // Parse album artists (prefer AlbumArtists, fallback to FirstAlbumArtist)
            var albumArtistNames = ParseArtists(tag.AlbumArtists, tag.FirstAlbumArtist);
            var albumArtists = await GetOrCreateArtistsAsync(albumArtistNames, cancellationToken);
            var primaryAlbumArtist = albumArtists[0];

            // Get or create album (using primary artist ID for lookup compatibility)
            var albumTitle = !string.IsNullOrWhiteSpace(tag.Album) ? tag.Album : "Unknown Album";
            var album = await GetOrCreateAlbumAsync(albumTitle, primaryAlbumArtist.Id, tag.Year, cancellationToken);

            // Update album artist relationships
            await UpdateAlbumArtistsAsync(album, albumArtists, cancellationToken);

            // Parse album genres (prefer Genres[], fallback to FirstGenre)
            var albumGenreNames = ParseGenres(tag.Genres, tag.FirstGenre);
            var albumGenres = await GetOrCreateGenresAsync(albumGenreNames, cancellationToken);
            await UpdateAlbumGenresAsync(album, albumGenres, cancellationToken);

            // Parse track artists (prefer Performers, fallback to FirstPerformer)
            var trackArtistNames = ParseArtists(tag.Performers, tag.FirstPerformer);
            var trackArtists = await GetOrCreateArtistsAsync(trackArtistNames, cancellationToken);
            var primaryTrackArtist = trackArtists[0];

            // Parse track genres (prefer Genres[], fallback to FirstGenre)
            var trackGenreNames = ParseGenres(tag.Genres, tag.FirstGenre);
            var trackGenres = await GetOrCreateGenresAsync(trackGenreNames, cancellationToken);

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
                        // Entity is already tracked, no need to call Update()
                    }
                }
                // If no embedded artwork, look for folder artwork
                else
                {
                    var folderArtworkPath = await FindAndSaveFolderArtworkAsync(filePath, album.Id);
                    if (folderArtworkPath != null)
                    {
                        album.CoverArtPath = folderArtworkPath;
                        // Entity is already tracked, no need to call Update()
                    }
                }
            }

            if (existingTrack != null)
            {
                // Update existing track
                existingTrack.Title = !string.IsNullOrWhiteSpace(tag.Title) ? tag.Title : Path.GetFileNameWithoutExtension(filePath);
                existingTrack.AlbumId = album.Id;
                existingTrack.TrackNumber = tag.Track > 0 ? (int)tag.Track : null;
                existingTrack.DiscNumber = tag.Disc > 0 ? (int)tag.Disc : null;
                existingTrack.DurationMs = (int)(properties.Duration.TotalMilliseconds);
                existingTrack.BitRate = properties.AudioBitrate;
                existingTrack.SampleRate = properties.AudioSampleRate;
                existingTrack.FilePath = filePath;
                existingTrack.FileSizeBytes = new FileInfo(filePath).Length;
                existingTrack.UpdatedAt = DateTime.UtcNow;

                // Update track artist relationships (this also sets ArtistId for backward compatibility)
                await UpdateTrackArtistsAsync(existingTrack, trackArtists, cancellationToken);

                // Update track genre relationships (this also sets Genre for backward compatibility)
                await UpdateTrackGenresAsync(existingTrack, trackGenres, cancellationToken);

                // Entity is already tracked from the Include query, no need to call Update()
                result.UpdatedTracks = 1;
                var artistNames = string.Join(", ", trackArtists.Select(a => a.Name));
                _logger.LogDebug("Updated track: {Title} by {Artists}", existingTrack.Title, artistNames);
            }
            else
            {
                // Create new track
                var track = new Track
                {
                    Title = !string.IsNullOrWhiteSpace(tag.Title) ? tag.Title : Path.GetFileNameWithoutExtension(filePath),
                    AlbumId = album.Id,
                    ArtistId = primaryTrackArtist.Id, // Set primary artist for backward compatibility
                    TrackNumber = tag.Track > 0 ? (int)tag.Track : null,
                    DiscNumber = tag.Disc > 0 ? (int)tag.Disc : null,
                    DurationMs = (int)(properties.Duration.TotalMilliseconds),
                    BitRate = properties.AudioBitrate,
                    SampleRate = properties.AudioSampleRate,
                    FilePath = filePath,
                    FileHash = fileHash,
                    FileSizeBytes = new FileInfo(filePath).Length,
                    Year = tag.Year > 0 ? (int)tag.Year : null
                };

                await _context.Tracks.AddAsync(track, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken); // Save to get track ID

                // Update track artist relationships (this also sets ArtistId for backward compatibility)
                await UpdateTrackArtistsAsync(track, trackArtists, cancellationToken);

                // Update track genre relationships (this also sets Genre for backward compatibility)
                await UpdateTrackGenresAsync(track, trackGenres, cancellationToken);

                result.NewTracks = 1;
                var artistNames = string.Join(", ", trackArtists.Select(a => a.Name));
                _logger.LogDebug("Added new track: {Title} by {Artists}", track.Title, artistNames);
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

    /// <summary>
    /// Gets or creates multiple artists in batch.
    /// </summary>
    /// <param name="artistNames">List of artist names to get or create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of Artist entities in the same order as input</returns>
    private async Task<List<Artist>> GetOrCreateArtistsAsync(List<string> artistNames, CancellationToken cancellationToken)
    {
        var artists = new List<Artist>();
        var normalizedNames = artistNames.Select(n => NormalizeString(n)).ToList();

        // Batch lookup existing artists
        var existingArtists = await _context.Artists
            .Where(a => normalizedNames.Contains(a.NameNormalized))
            .ToListAsync(cancellationToken);

        var existingByNormalized = existingArtists.ToDictionary(a => a.NameNormalized);

        // Create missing artists
        var artistsToCreate = new List<Artist>();
        for (int i = 0; i < artistNames.Count; i++)
        {
            var normalized = normalizedNames[i];
            var name = artistNames[i];

            if (existingByNormalized.TryGetValue(normalized, out var existingArtist))
            {
                artists.Add(existingArtist);
            }
            else
            {
                var newArtist = new Artist
                {
                    Name = name,
                    SortName = GetSortName(name),
                    NameNormalized = normalized
                };
                artistsToCreate.Add(newArtist);
                artists.Add(newArtist);
                // Update dictionary to prevent duplicate creation for case-variant duplicates in the same input list
                existingByNormalized[normalized] = newArtist;
            }
        }

        // Batch create new artists
        if (artistsToCreate.Count > 0)
        {
            await _context.Artists.AddRangeAsync(artistsToCreate, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken); // Save immediately to prevent duplicates
            _logger.LogDebug("Created {Count} new artists", artistsToCreate.Count);
        }

        return artists;
    }

    /// <summary>
    /// Gets or creates multiple genres in batch.
    /// </summary>
    /// <param name="genreNames">List of genre names to get or create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of Genre entities in the same order as input</returns>
    private async Task<List<Genre>> GetOrCreateGenresAsync(List<string> genreNames, CancellationToken cancellationToken)
    {
        if (genreNames.Count == 0)
        {
            return new List<Genre>();
        }

        var genres = new List<Genre>();
        var normalizedNames = genreNames.Select(n => NormalizeString(n)).ToList();

        // Batch lookup existing genres
        var existingGenres = await _context.Genres
            .Where(g => normalizedNames.Contains(g.NormalizedName))
            .ToListAsync(cancellationToken);

        var existingByNormalized = existingGenres.ToDictionary(g => g.NormalizedName);

        // Create missing genres
        var genresToCreate = new List<Genre>();
        for (int i = 0; i < genreNames.Count; i++)
        {
            var normalized = normalizedNames[i];
            var name = genreNames[i];

            if (existingByNormalized.TryGetValue(normalized, out var existingGenre))
            {
                genres.Add(existingGenre);
            }
            else
            {
                var newGenre = new Genre
                {
                    Name = name,
                    NormalizedName = normalized
                };
                genresToCreate.Add(newGenre);
                genres.Add(newGenre);
                // Update dictionary to prevent duplicate creation for case-variant duplicates in the same input list
                existingByNormalized[normalized] = newGenre;
            }
        }

        // Batch create new genres
        if (genresToCreate.Count > 0)
        {
            await _context.Genres.AddRangeAsync(genresToCreate, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken); // Save immediately to prevent duplicates
            _logger.LogDebug("Created {Count} new genres", genresToCreate.Count);
        }

        return genres;
    }

    /// <summary>
    /// Updates TrackArtist relationships for a track, adding new ones and removing old ones.
    /// Sets the primary artist (first in list) to track.ArtistId for backward compatibility.
    /// </summary>
    private async Task UpdateTrackArtistsAsync(Track track, List<Artist> artists, CancellationToken cancellationToken)
    {
        // Ensure track has at least one artist
        if (artists.Count == 0)
        {
            var unknownArtist = await GetOrCreateArtistAsync("Unknown Artist", cancellationToken);
            artists = new List<Artist> { unknownArtist };
        }

        // Set primary artist for backward compatibility
        track.ArtistId = artists[0].Id;

        // Load existing TrackArtists if not already loaded
        if (track.TrackArtists == null || !_context.Entry(track).Collection(t => t.TrackArtists).IsLoaded)
        {
            await _context.Entry(track)
                .Collection(t => t.TrackArtists)
                .LoadAsync(cancellationToken);
        }

        var existingArtistIds = track.TrackArtists.Select(ta => ta.ArtistId).ToHashSet();
        var newArtistIds = artists.Select(a => a.Id).ToHashSet();

        // Remove relationships that are no longer in the new list
        var toRemove = track.TrackArtists
            .Where(ta => !newArtistIds.Contains(ta.ArtistId))
            .ToList();

        foreach (var trackArtist in toRemove)
        {
            _context.Remove(trackArtist);
        }

        // Add new relationships that don't exist
        var toAdd = artists
            .Where(a => !existingArtistIds.Contains(a.Id))
            .Select(a => new TrackArtist
            {
                TrackId = track.Id,
                ArtistId = a.Id
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _context.TrackArtists.AddRangeAsync(toAdd, cancellationToken);
        }

        if (toRemove.Count > 0 || toAdd.Count > 0)
        {
            _logger.LogDebug("Updated TrackArtist relationships for track {TrackId}: removed {Removed}, added {Added}", 
                track.Id, toRemove.Count, toAdd.Count);
        }
    }

    /// <summary>
    /// Updates AlbumArtist relationships for an album, adding new ones and removing old ones.
    /// Sets the primary artist (first in list) to album.ArtistId for backward compatibility.
    /// </summary>
    private async Task UpdateAlbumArtistsAsync(Album album, List<Artist> artists, CancellationToken cancellationToken)
    {
        // Ensure album has at least one artist
        if (artists.Count == 0)
        {
            var unknownArtist = await GetOrCreateArtistAsync("Unknown Artist", cancellationToken);
            artists = new List<Artist> { unknownArtist };
        }

        // Set primary artist for backward compatibility
        album.ArtistId = artists[0].Id;

        // Load existing AlbumArtists if not already loaded
        if (album.AlbumArtists == null || !_context.Entry(album).Collection(a => a.AlbumArtists).IsLoaded)
        {
            await _context.Entry(album)
                .Collection(a => a.AlbumArtists)
                .LoadAsync(cancellationToken);
        }

        var existingArtistIds = album.AlbumArtists.Select(aa => aa.ArtistId).ToHashSet();
        var newArtistIds = artists.Select(a => a.Id).ToHashSet();

        // Remove relationships that are no longer in the new list
        var toRemove = album.AlbumArtists
            .Where(aa => !newArtistIds.Contains(aa.ArtistId))
            .ToList();

        foreach (var albumArtist in toRemove)
        {
            _context.Remove(albumArtist);
        }

        // Add new relationships that don't exist
        var toAdd = artists
            .Where(a => !existingArtistIds.Contains(a.Id))
            .Select(a => new AlbumArtist
            {
                AlbumId = album.Id,
                ArtistId = a.Id
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _context.AlbumArtists.AddRangeAsync(toAdd, cancellationToken);
        }

        if (toRemove.Count > 0 || toAdd.Count > 0)
        {
            _logger.LogDebug("Updated AlbumArtist relationships for album {AlbumId}: removed {Removed}, added {Added}", 
                album.Id, toRemove.Count, toAdd.Count);
        }
    }

    /// <summary>
    /// Updates TrackGenre relationships for a track, adding new ones and removing old ones.
    /// Sets the primary genre (first in list) to track.Genre for backward compatibility.
    /// </summary>
    private async Task UpdateTrackGenresAsync(Track track, List<Genre> genres, CancellationToken cancellationToken)
    {
        // Set primary genre for backward compatibility (or null if empty)
        track.Genre = genres.Count > 0 ? genres[0].Name : null;

        // Load existing TrackGenres if not already loaded
        if (track.TrackGenres == null || !_context.Entry(track).Collection(t => t.TrackGenres).IsLoaded)
        {
            await _context.Entry(track)
                .Collection(t => t.TrackGenres)
                .LoadAsync(cancellationToken);
        }

        var existingGenreIds = track.TrackGenres.Select(tg => tg.GenreId).ToHashSet();
        var newGenreIds = genres.Select(g => g.Id).ToHashSet();

        // Remove relationships that are no longer in the new list
        var toRemove = track.TrackGenres
            .Where(tg => !newGenreIds.Contains(tg.GenreId))
            .ToList();

        foreach (var trackGenre in toRemove)
        {
            _context.Remove(trackGenre);
        }

        // Add new relationships that don't exist
        var toAdd = genres
            .Where(g => !existingGenreIds.Contains(g.Id))
            .Select(g => new TrackGenre
            {
                TrackId = track.Id,
                GenreId = g.Id
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _context.TrackGenres.AddRangeAsync(toAdd, cancellationToken);
        }

        if (toRemove.Count > 0 || toAdd.Count > 0)
        {
            _logger.LogDebug("Updated TrackGenre relationships for track {TrackId}: removed {Removed}, added {Added}", 
                track.Id, toRemove.Count, toAdd.Count);
        }
    }

    /// <summary>
    /// Updates AlbumGenre relationships for an album, adding new ones and removing old ones.
    /// Sets the primary genre (first in list) to album.Genre for backward compatibility.
    /// </summary>
    private async Task UpdateAlbumGenresAsync(Album album, List<Genre> genres, CancellationToken cancellationToken)
    {
        // Set primary genre for backward compatibility (or null if empty)
        album.Genre = genres.Count > 0 ? genres[0].Name : null;

        // Load existing AlbumGenres if not already loaded
        if (album.AlbumGenres == null || !_context.Entry(album).Collection(a => a.AlbumGenres).IsLoaded)
        {
            await _context.Entry(album)
                .Collection(a => a.AlbumGenres)
                .LoadAsync(cancellationToken);
        }

        var existingGenreIds = album.AlbumGenres.Select(ag => ag.GenreId).ToHashSet();
        var newGenreIds = genres.Select(g => g.Id).ToHashSet();

        // Remove relationships that are no longer in the new list
        var toRemove = album.AlbumGenres
            .Where(ag => !newGenreIds.Contains(ag.GenreId))
            .ToList();

        foreach (var albumGenre in toRemove)
        {
            _context.Remove(albumGenre);
        }

        // Add new relationships that don't exist
        var toAdd = genres
            .Where(g => !existingGenreIds.Contains(g.Id))
            .Select(g => new AlbumGenre
            {
                AlbumId = album.Id,
                GenreId = g.Id
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _context.AlbumGenres.AddRangeAsync(toAdd, cancellationToken);
        }

        if (toRemove.Count > 0 || toAdd.Count > 0)
        {
            _logger.LogDebug("Updated AlbumGenre relationships for album {AlbumId}: removed {Removed}, added {Added}", 
                album.Id, toRemove.Count, toAdd.Count);
        }
    }

    private async Task<Album> GetOrCreateAlbumAsync(string title, string artistId, uint year, CancellationToken cancellationToken)
    {
        var normalized = NormalizeString(title);
        var album = await _context.Albums
            .Include(a => a.AlbumArtists)
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
        var baseDir = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
            ? "/data"
            : Path.Combine(Directory.GetCurrentDirectory(), "Data");
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

    private Task<string?> SaveCoverArtFromFileAsync(string sourceFilePath, string albumId)
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

            return Task.FromResult<string?>($"/artwork/{fileName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving folder cover art for album: {AlbumId}", albumId);
            return Task.FromResult<string?>(null);
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

    /// <summary>
    /// Parses artist names from TagLib tags, preferring native multi-valued tags over delimiter parsing.
    /// </summary>
    /// <param name="artists">Native multi-valued artists array from tag (e.g., tag.Performers or tag.AlbumArtists)</param>
    /// <param name="singleValue">Single-value fallback (e.g., tag.FirstPerformer or tag.FirstAlbumArtist)</param>
    /// <returns>List of artist names, trimmed and deduplicated</returns>
    private List<string> ParseArtists(string[] artists, string? singleValue)
    {
        var result = new List<string>();
        bool usedNativeTags = false;

        // Priority 1: Use native multi-valued tags if arrays are non-empty
        if (artists != null && artists.Length > 0)
        {
            var validArtists = artists
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .Distinct()
                .ToList();

            if (validArtists.Count > 0)
            {
                result = validArtists;
                usedNativeTags = true;
                if (result.Count > 1)
                {
                    _logger.LogDebug("Found {Count} artists from native multi-valued tags", result.Count);
                }
            }
        }

        // Priority 2: If arrays are empty, fallback to single-value tags
        if (result.Count == 0 && !string.IsNullOrWhiteSpace(singleValue))
        {
            var trimmedValue = singleValue.Trim();

            // Priority 3: If single value contains delimiters and delimiter parsing is enabled, parse by delimiter
            if (_multiValuedTagsOptions.EnableDelimiterParsing && trimmedValue.Length > 0)
            {
                // Try delimiters in order of preference
                char? delimiter = null;
                foreach (var delim in _multiValuedTagsOptions.PreferredDelimiters)
                {
                    if (delim.Length == 1 && trimmedValue.Contains(delim[0]))
                    {
                        delimiter = delim[0];
                        break;
                    }
                }

                if (delimiter.HasValue)
                {
                    result = trimmedValue
                        .Split(delimiter.Value, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.Trim())
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .Distinct()
                        .ToList();

                    if (result.Count > 1)
                    {
                        _logger.LogDebug("Parsed {Count} artists from delimiter-separated value using '{Delimiter}' delimiter", result.Count, delimiter.Value);
                    }
                }
            }

            // If no delimiter found or delimiter parsing disabled, treat as single artist
            if (result.Count == 0)
            {
                result = new List<string> { trimmedValue };
            }
        }

        // Fallback to "Unknown Artist" if no artists found
        if (result.Count == 0)
        {
            result = new List<string> { "Unknown Artist" };
        }

        return result;
    }

    /// <summary>
    /// Parses genre names from TagLib tags, preferring native multi-valued tags over delimiter parsing.
    /// </summary>
    /// <param name="genres">Native multi-valued genres array from tag (e.g., tag.Genres)</param>
    /// <param name="singleValue">Single-value fallback (e.g., tag.FirstGenre)</param>
    /// <returns>List of genre names, trimmed and deduplicated. Returns empty list if no genres found (unlike artists, genres can be null/empty)</returns>
    private List<string> ParseGenres(string[] genres, string? singleValue)
    {
        var result = new List<string>();

        // Priority 1: Use native multi-valued tags if arrays are non-empty
        if (genres != null && genres.Length > 0)
        {
            var validGenres = genres
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => g.Trim())
                .Distinct()
                .ToList();

            if (validGenres.Count > 0)
            {
                result = validGenres;
                if (result.Count > 1)
                {
                    _logger.LogDebug("Found {Count} genres from native multi-valued tags", result.Count);
                }
            }
        }

        // Priority 2: If arrays are empty, fallback to single-value tags
        if (result.Count == 0 && !string.IsNullOrWhiteSpace(singleValue))
        {
            var trimmedValue = singleValue.Trim();

            // Priority 3: If single value contains delimiters and delimiter parsing is enabled, parse by delimiter
            if (_multiValuedTagsOptions.EnableDelimiterParsing && trimmedValue.Length > 0)
            {
                // Try delimiters in order of preference
                char? delimiter = null;
                foreach (var delim in _multiValuedTagsOptions.PreferredDelimiters)
                {
                    if (delim.Length == 1 && trimmedValue.Contains(delim[0]))
                    {
                        delimiter = delim[0];
                        break;
                    }
                }

                if (delimiter.HasValue)
                {
                    result = trimmedValue
                        .Split(delimiter.Value, StringSplitOptions.RemoveEmptyEntries)
                        .Select(g => g.Trim())
                        .Where(g => !string.IsNullOrWhiteSpace(g))
                        .Distinct()
                        .ToList();

                    if (result.Count > 1)
                    {
                        _logger.LogDebug("Parsed {Count} genres from delimiter-separated value using '{Delimiter}' delimiter", result.Count, delimiter.Value);
                    }
                }
            }

            // If no delimiter found or delimiter parsing disabled, treat as single genre
            if (result.Count == 0)
            {
                result = new List<string> { trimmedValue };
            }
        }

        // Unlike artists, genres can be null/empty - return empty list if no genres found
        return result;
    }
}