using TagLib;
using MusicServer.Data;
using MusicServer.Models;
using Microsoft.EntityFrameworkCore;


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

            // 1️⃣ Get all audio files in the library path
            var audioFiles = Directory.GetFiles(_libraryPath, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".flac", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 2️⃣ Get existing database records
            var dbTracks = _dbContext.Tracks.ToList();
            var dbFilePaths = dbTracks.Select(t => t.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 3️⃣ Identify new and removed files
            var newFiles = audioFiles.Where(file => !dbFilePaths.Contains(file)).ToList();
            var removedTracks = dbTracks.Where(track => !audioFiles.Contains(track.FilePath)).ToList();

            _logger.LogInformation("Found {NewCount} new files and {RemovedCount} removed files.", newFiles.Count, removedTracks.Count);

            // 4️⃣ Process new files
            foreach (var file in newFiles)
            {
                try
                {
                    var tagFile = TagLib.File.Create(file);

                    string trackTitle = string.IsNullOrEmpty(tagFile.Tag.Title) ? Path.GetFileNameWithoutExtension(file) : tagFile.Tag.Title;
                    string albumArtistName = tagFile.Tag.AlbumArtists.FirstOrDefault() ?? "Unknown Album Artist";
                    string albumName = tagFile.Tag.Album ?? "Unknown Album";
                    int? releaseYear = tagFile.Tag.Year > 0 ? (int?)tagFile.Tag.Year : null;
                    string genre = tagFile.Tag.Genres.FirstOrDefault() ?? "Unknown Genre";
                    int? trackNumber = tagFile.Tag.Track > 0 ? (int?)tagFile.Tag.Track : null;
                    string fileFormat = Path.GetExtension(file).TrimStart('.').ToUpper();
                    int bitrate = tagFile.Properties.AudioBitrate;
                    long fileSize = new FileInfo(file).Length;

                    // 5️⃣ Insert/Get Artist (using Album Artist)
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


                    // 6️⃣ Insert/Get Album
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
                            CoverArtUrl = GetCoverArtPath(file)
                        };

                        _dbContext.Albums.Add(album);
                        _dbContext.SaveChanges();
                    }


                    // 7️⃣ Insert Track
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

            // 8️⃣ Remove deleted tracks
            foreach (var track in removedTracks)
            {
                _dbContext.Tracks.Remove(track);
                _logger.LogInformation("Removed track: {TrackTitle}", track.Title);
            }

            // 9️⃣ Save changes
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

        private string GetCoverArtPath(string filePath)
        {
            var albumDirectory = Path.GetDirectoryName(filePath);
            return Directory.GetFiles(albumDirectory, "cover.*").FirstOrDefault() ??
                Directory.GetFiles(albumDirectory, "folder.*").FirstOrDefault() ??
                Directory.GetFiles(albumDirectory, "*.jpg").FirstOrDefault() ??
                Directory.GetFiles(albumDirectory, "*.png").FirstOrDefault() ?? "";
        }


    }
}