using TagLib;
using MusicServer.Data;
using MusicServer.Models;

namespace MusicServer.Services
{
    public class LibraryScanner
    {
        private readonly string _libraryPath;
        private readonly ILogger<LibraryScanner> _logger;
        private readonly MusicDbContext _dbContext;

        public LibraryScanner(IConfiguration config, ILogger<LibraryScanner> logger, MusicDbContext dbContext)
        {
            _libraryPath = Environment.GetEnvironmentVariable("MUSIC_LIBRARY_PATH") ?? "/music";
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public void ScanLibrary()
        {
            if (!Directory.Exists(_libraryPath))
            {
                _logger.LogWarning("Music library path '{LibraryPath}' does not exist.", _libraryPath);
                return;
            }

            // 1️ Get all audio files
            var audioFiles = Directory.GetFiles(_libraryPath, "*.*", SearchOption.AllDirectories)
                                    .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                                            || f.EndsWith(".flac", StringComparison.OrdinalIgnoreCase)
                                            || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                                            || f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                                    .ToList();

            // 2️ Get database records
            var dbTracks = _dbContext.Tracks.ToList();
            var dbFilePaths = dbTracks.Select(t => t.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 3️ Determine new and removed files
            var newFiles = audioFiles.Where(file => !dbFilePaths.Contains(file)).ToList();
            var audioFilesSet = audioFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removedTracks = dbTracks.Where(track => !audioFilesSet.Contains(track.FilePath)).ToList();

            _logger.LogInformation("Found {NewCount} new files and {RemovedCount} removed files.",
                newFiles.Count, removedTracks.Count);

            // 4️ Process new files
            foreach (var file in newFiles)
            {
                try
                {
                    var tagFile = TagLib.File.Create(file);

                    // Extract metadata
                    string trackTitle = string.IsNullOrEmpty(tagFile.Tag.Title) ? Path.GetFileNameWithoutExtension(file) : tagFile.Tag.Title;
                    string artistName = tagFile.Tag.Performers.FirstOrDefault() ?? "Unknown Artist";
                    string albumName = tagFile.Tag.Album ?? "Unknown Album";
                    string albumArtistName = tagFile.Tag.AlbumArtists.FirstOrDefault() ?? artistName;
                    int? releaseYear = tagFile.Tag.Year > 0 ? (int?)tagFile.Tag.Year : null;
                    string genre = tagFile.Tag.Genres.FirstOrDefault() ?? "Unknown Genre";
                    int? trackNumber = tagFile.Tag.Track > 0 ? (int?)tagFile.Tag.Track : null;
                    string fileFormat = Path.GetExtension(file).TrimStart('.').ToUpper();
                    int bitrate = tagFile.Properties.AudioBitrate;
                    long fileSize = new FileInfo(file).Length;
                    // Extract cover art path (look for common cover image files)
                    var albumDirectory = Path.GetDirectoryName(file);
                    var coverArtPath = Directory.GetFiles(albumDirectory, "cover.*").FirstOrDefault() ??
                                    Directory.GetFiles(albumDirectory, "folder.*").FirstOrDefault() ??
                                    Directory.GetFiles(albumDirectory, "*.jpg").FirstOrDefault() ??
                                    Directory.GetFiles(albumDirectory, "*.png").FirstOrDefault() ??
                                    "";  // Default to empty string if no cover found


                    // 5️ Insert or get Artist
                    // Normalize the album artist name to prevent duplicates
                    var normalizedAlbumArtist = albumArtistName.Trim();
                    
                    // Check if the artist already exists
                    var albumArtist = _dbContext.Artists.FirstOrDefault(a => a.Name == normalizedAlbumArtist);
                    if (albumArtist == null)
                    {
                        albumArtist = new Artist
                        {
                            Name = normalizedAlbumArtist,
                            SortName = GetSortName(normalizedAlbumArtist) // e.g., "Weeknd, The"
                        };
                        _dbContext.Artists.Add(albumArtist);
                        _dbContext.SaveChanges();
                    }


                    // 6️ Insert or get Album
                    // Ensure we get or create a single artist for the album artist
                    if (albumArtist == null)
                    {
                        albumArtist = new Artist
                        {
                            Name = albumArtistName,
                            SortName = GetSortName(albumArtistName)
                        };
                        _dbContext.Artists.Add(albumArtist);
                        _dbContext.SaveChanges();
                    }

                    // Check if the album already exists before inserting
                    var album = _dbContext.Albums
                        .FirstOrDefault(a => a.Name == albumName && a.ArtistId == artist.Id);

                    if (album == null)
                    {
                        album = new Album
                        {
                            Name = albumName,
                            ArtistId = artist.Id,
                            ReleaseYear = tagFile.Tag.Year > 0 ? (int?)tagFile.Tag.Year : null,
                            Genre = tagFile.Tag.Genres.FirstOrDefault() ?? "Unknown Genre",
                            CoverArtUrl = coverArtPath // Ensure this is set correctly
                        };

                        _dbContext.Albums.Add(album);
                        _dbContext.SaveChanges(); // Save to ensure the ID is assigned
                    }

                    // 7️ Insert Track
                    var track = new Track
                    {
                        Title = trackTitle,
                        ArtistId = albumArtist.Id,
                        AlbumId = album.Id,
                        TrackNumber = trackNumber ?? 0,
                        Duration = tagFile.Properties.Duration.ToString(@"mm\:ss"),
                        FileFormat = fileFormat,
                        Bitrate = bitrate,
                        FileSize = fileSize,
                        FilePath = file
                    };

                    _dbContext.Tracks.Add(track);
                    _logger.LogInformation("Added track: {TrackTitle} by {Artist}", track.Title, albumArtist.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file: {File}", file);
                }
            }

            // 8️ Remove deleted tracks
            foreach (var track in removedTracks)
            {
                _dbContext.Tracks.Remove(track);
                _logger.LogInformation("Removed track: {TrackTitle}", track.Title);
            }

            // 9️ Save database changes
            _dbContext.SaveChanges();
            _logger.LogInformation("Library scan complete.");
        }

        // Helper method to generate SortName for artists
        private string GetSortName(string name)
        {
            if (name.StartsWith("The "))
            {
                return $"{name.Substring(4)}, The";
            }
            return name;
        }

    }
}