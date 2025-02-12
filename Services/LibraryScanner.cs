using Microsoft.EntityFrameworkCore;
using MusicServer.Data;
using MusicServer.Models;
using TagLib;

namespace MusicServer.Services
{
    /// <summary>
    /// Scans the music library directory for audio files, updates the database with new tracks,
    /// and removes tracks that are no longer present.
    /// </summary>
    public class LibraryScanner
    {
        // Private fields for the library path, logger, and database context.
        private readonly string _libraryPath;
        private readonly ILogger<LibraryScanner> _logger;
        private readonly MusicDbContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="LibraryScanner"/> class.
        /// </summary>
        /// <param name="config">The application configuration.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="dbContext">The music database context.</param>
        public LibraryScanner(IConfiguration config, ILogger<LibraryScanner> logger, MusicDbContext dbContext)
        {
            _libraryPath = Environment.GetEnvironmentVariable("MUSIC_LIBRARY_PATH") ?? "/music";
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <summary>
        /// Scans the music library, processes new audio files, and removes deleted tracks.
        /// </summary>
        public void ScanLibrary()
        {
            // Ensure the music library path exists
            if (!Directory.Exists(_libraryPath))
            {
                _logger.LogWarning("Music library path '{LibraryPath}' does not exist.", _libraryPath);
                return;
            }

            // 1️. Get all audio files in the library path
            var audioFiles = GetAudioFiles();

            // 2️. Get existing database records
            var dbTracks = _dbContext.Tracks.ToList();
            var dbFilePaths = dbTracks.Select(t => t.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 3️. Identify new and removed files
            var newFiles = audioFiles.Where(file => !dbFilePaths.Contains(file)).ToList();
            var removedTracks = dbTracks.Where(track => !audioFiles.Contains(track.FilePath)).ToList();

            _logger.LogInformation("Found {NewCount} new files and {RemovedCount} removed files.", newFiles.Count, removedTracks.Count);

            // 4️. Process new files
            foreach (var file in newFiles)
            {
                try
                {
                    var tagFile = TagLib.File.Create(file);

                    // Extract metadata from the audio file.
                    string trackTitle = string.IsNullOrEmpty(tagFile.Tag.Title) 
                        ? Path.GetFileNameWithoutExtension(file) 
                        : tagFile.Tag.Title;
                    string albumArtistName = tagFile.Tag.AlbumArtists.FirstOrDefault() ?? "Unknown Album Artist";
                    string albumName = tagFile.Tag.Album ?? "Unknown Album";
                    int? releaseYear = tagFile.Tag.Year > 0 ? (int?)tagFile.Tag.Year : null;
                    string genre = tagFile.Tag.Genres.FirstOrDefault() ?? "Unknown Genre";
                    int? trackNumber = tagFile.Tag.Track > 0 ? (int?)tagFile.Tag.Track : null;
                    int? discNumber = tagFile.Tag.Disc > 0 ? (int?)tagFile.Tag.Disc : null;
                    string fileFormat = Path.GetExtension(file).TrimStart('.').ToUpper();
                    int bitrate = tagFile.Properties.AudioBitrate;
                    long fileSize = new FileInfo(file).Length;

                    // 5️. Insert/Get Artist (using Album Artist)
                    var albumArtist = _dbContext.Artists
                        .AsEnumerable() // Switch to LINQ to Objects
                        .FirstOrDefault(a => string.Equals(a.Name, albumArtistName, StringComparison.OrdinalIgnoreCase));


                    if (albumArtist == null)
                    {
                        albumArtist = new Artist
                        {
                            Name = albumArtistName,
                            SortName = GetSortName(albumArtistName)
                        };

                        try
                        {
                            _dbContext.Artists.Add(albumArtist);
                            _dbContext.SaveChanges();
                        }
                        catch (DbUpdateException ex) // Handle race conditions
                        {
                            _dbContext.Entry(albumArtist).State = EntityState.Detached; // Avoid tracking duplicate entity
                            albumArtist = _dbContext.Artists.AsEnumerable().FirstOrDefault(a => string.Equals(a.Name, albumArtistName, StringComparison.OrdinalIgnoreCase));
                            Console.WriteLine(ex);
                            if (albumArtist == null) // If still null, rethrow the error
                                throw;
                        }
                    }


                    // 6️. Insert/Get Album
                    var album = _dbContext.Albums
                        .FirstOrDefault(a => a.Name == albumName && a.ArtistId == albumArtist.Id);

                    if (album == null)
                    {
                        album = new Album
                        {
                            Name = albumName,
                            ArtistId = albumArtist.Id,
                            ReleaseYear = releaseYear,
                            Genre = genre,
                            CoverArtUrl = GetCoverArtPath(file),
                            DateAdded = DateTime.UtcNow // Record the time when the album is added.
                        };

                        _dbContext.Albums.Add(album);
                        _dbContext.SaveChanges();
                    }


                    // 7️. Insert Track
                    var track = new Track
                    {
                        Title = trackTitle,
                        ArtistId = albumArtist.Id,
                        AlbumId = album.Id,
                        DiscNumber = discNumber ?? 1,
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

            // 8️. Remove deleted tracks
            foreach (var track in removedTracks)
            {
                _dbContext.Tracks.Remove(track);
                _logger.LogInformation("Removed track: {TrackTitle}", track.Title);
            }

            // 9️. Save changes
            _dbContext.SaveChanges();
            _logger.LogInformation("Library scan complete.");
        }

        /// <summary>
        /// Retrieves a list of audio file paths from the music library.
        /// </summary>
        /// <returns>A list of audio file paths.</returns>
        public List<string> GetAudioFiles()
        {
            // Define the allowed audio extensions.
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mp3", ".flac", ".wav", ".ogg"
            };

            var audioFiles = new DirectoryInfo(_libraryPath)
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Where(file =>
                    // Exclude files with the hidden attribute.
                    !file.Attributes.HasFlag(FileAttributes.Hidden)
                    // Exclude macOS resource fork files.
                    && !file.Name.StartsWith("._", StringComparison.Ordinal)
                    // Only include allowed audio file types.
                    && allowedExtensions.Contains(file.Extension)
                )
                .Select(file => file.FullName)
                .ToList();

            return audioFiles;
        }

        #region Helper Methods

        /// <summary>
        /// Generates a sortable version of an artist's name.
        /// </summary>
        /// <param name="name">The original artist name.</param>
        /// <returns>A sortable artist name.</returns>
        private string GetSortName(string name)
        {
            if (name.StartsWith("The "))
            {
                return $"{name.Substring(4)}, The";
            }
            return name;
        }

        /// <summary>
        /// Attempts to locate cover art in the album directory based on common naming patterns.
        /// </summary>
        /// <param name="filePath">The path of the audio file.</param>
        /// <returns>The file path to the cover art image, or an empty string if none is found.</returns>
        private string GetCoverArtPath(string filePath)
        {
            var albumDirectory = Path.GetDirectoryName(filePath);
            return Directory.GetFiles(albumDirectory, "cover.*").FirstOrDefault() ??
                Directory.GetFiles(albumDirectory, "folder.*").FirstOrDefault() ??
                Directory.GetFiles(albumDirectory, "*.jpg").FirstOrDefault() ??
                Directory.GetFiles(albumDirectory, "*.png").FirstOrDefault() ?? "";
        }

        #endregion
    }
}