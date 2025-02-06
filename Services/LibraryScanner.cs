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
            _libraryPath = config.GetValue<string>("MusicLibraryPath") ?? throw new ArgumentNullException(nameof(_libraryPath));
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

            // 1. Get all audio files from the file system.
            // You can adjust the filter to include other formats as needed.
            var audioFiles = Directory.GetFiles(_libraryPath, "*.*", SearchOption.AllDirectories)
                                      .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                                               || f.EndsWith(".flac", StringComparison.OrdinalIgnoreCase)
                                               || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                                               || f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                                      .ToList();

            // 2. Get the list of file paths currently in the database.
            var dbTracks = _dbContext.MusicTracks.ToList();
            var dbFilePaths = dbTracks.Select(t => t.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 3. Determine new files (present in file system but not in the database).
            var newFiles = audioFiles.Where(file => !dbFilePaths.Contains(file)).ToList();

            // 4. Determine removed files (present in the database but missing from file system).
            var audioFilesSet = audioFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removedTracks = dbTracks.Where(track => !audioFilesSet.Contains(track.FilePath)).ToList();

            _logger.LogInformation("Found {NewCount} new files and {RemovedCount} removed files.",
                newFiles.Count, removedTracks.Count);

            // 5. Process and add new files.
            foreach (var file in newFiles)
            {
                try
                {
                    var tagFile = TagLib.File.Create(file);

                    var track = new MusicTrack
                    {
                        FilePath = file,
                        TrackTitle = string.IsNullOrEmpty(tagFile.Tag.Title)
                                     ? Path.GetFileNameWithoutExtension(file)
                                     : tagFile.Tag.Title,
                        Artist = tagFile.Tag.Performers.FirstOrDefault() ?? "Unknown Artist",
                        AlbumName = tagFile.Tag.Album ?? "Unknown Album",
                        AlbumArtist = tagFile.Tag.AlbumArtists.FirstOrDefault() ?? "Unknown Album Artist",
                        ReleaseYear = tagFile.Tag.Year > 0 ? (int?)tagFile.Tag.Year : null,
                        Genre = tagFile.Tag.Genres.FirstOrDefault() ?? "Unknown Genre",
                        TrackNumber = tagFile.Tag.Track > 0 ? (int?)tagFile.Tag.Track : null,
                        Duration = tagFile.Properties.Duration,
                        FileFormat = Path.GetExtension(file).TrimStart('.').ToUpper(),
                        Bitrate = tagFile.Properties.AudioBitrate,
                        FileSize = new FileInfo(file).Length,
                        MusicBrainzId = tagFile.Tag.MusicBrainzTrackId ?? string.Empty,
                        ReleaseType = tagFile.Tag.Grouping ?? "Unknown",
                        AlbumType = tagFile.Tag.AlbumSort ?? "Unknown"
                    };

                    _dbContext.MusicTracks.Add(track);
                    _logger.LogInformation("Added new track: {TrackTitle} by {Artist}", track.TrackTitle, track.Artist);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file: {File}", file);
                }
            }

            // 6. Remove tracks that are no longer present.
            foreach (var track in removedTracks)
            {
                _dbContext.MusicTracks.Remove(track);
                _logger.LogInformation("Removed track: {TrackTitle} by {Artist}", track.TrackTitle, track.Artist);
            }

            // 7. Save changes to the database.
            _dbContext.SaveChanges();
            _logger.LogInformation("Library scan complete.");
        }
    }
}