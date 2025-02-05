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
                _logger.LogError("The specified library path does not exist: {LibraryPath}", _libraryPath);
                return;
            }

            var audioFiles = Directory.GetFiles(_libraryPath, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".mp3") || f.EndsWith(".flac") || f.EndsWith(".wav") || f.EndsWith(".ogg"))
                .ToArray();

            if (audioFiles.Length == 0)
            {
                _logger.LogInformation("No audio files found in the directory: {LibraryPath}", _libraryPath);
                return;
            }

            foreach (var file in audioFiles)
            {
                try
                {
                    var tagFile = TagLib.File.Create(file);

                    var musicTrack = new MusicTrack
                    {
                        FilePath = file,
                        TrackTitle = tagFile.Tag.Title,
                        Artist = string.Join(", ", tagFile.Tag.Performers),
                        AlbumName = tagFile.Tag.Album,
                        AlbumArtist = string.Join(", ", tagFile.Tag.AlbumArtists),
                        ReleaseYear = (int?)tagFile.Tag.Year,
                        Genre = string.Join(", ", tagFile.Tag.Genres),
                        TrackNumber = (int?)tagFile.Tag.Track,
                        Duration = tagFile.Properties.Duration,
                        FileFormat = Path.GetExtension(file).TrimStart('.').ToUpper(),
                        Bitrate = tagFile.Properties.AudioBitrate,
                        FileSize = new FileInfo(file).Length,
                        MusicBrainzId = tagFile.Tag.MusicBrainzTrackId,
                        ReleaseType = tagFile.Tag.Grouping, // This depends on the metadata format
                        AlbumType = tagFile.Tag.AlbumSort, // Placeholder, adjust as needed
                    };

                    _dbContext.MusicTracks.Add(musicTrack);
                    _logger.LogInformation("Added track: {TrackTitle} by {Artist}", musicTrack.TrackTitle, musicTrack.Artist);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error processing file {File}: {Message}", file, ex.Message);
                }
            }

            _dbContext.SaveChanges();
            _logger.LogInformation("Library scan complete. {TrackCount} tracks added.", audioFiles.Length);
        }
    }
}