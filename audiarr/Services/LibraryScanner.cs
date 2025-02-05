using TagLib;
using MusicServer.Data;
using MusicServer.Models;

namespace MusicServer.Services
{
    public class LibraryScanner
    {
        private readonly MusicDbContext _dbContext;
        private readonly string _musicDirectory;

        public LibraryScanner(MusicDbContext dbContext, IConfiguration config)
        {
            _dbContext = dbContext;
            _musicDirectory = config["MusicLibraryPath"] ?? throw new Exception("MusicLibraryPath is not set in appsettings.json");
        }

        public void ScanLibrary()
        {
            var musicFiles = Directory.GetFiles(_musicDirectory, "*.flac", SearchOption.AllDirectories);

            foreach (var file in musicFiles)
            {
                var tagFile = TagLib.File.Create(file);

                var track = new Track
                {
                    Title = tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(file),
                    Artist = tagFile.Tag.FirstPerformer ?? "Unknown Artist",
                    Album = tagFile.Tag.Album ?? "Unknown Album",
                    Year = (int?)tagFile.Tag.Year,
                    FilePath = file
                };

                // Avoid duplicate entries
                if (!_dbContext.Tracks.Any(t => t.FilePath == file))
                {
                    _dbContext.Tracks.Add(track);
                    Console.WriteLine(track.ToString());
                }
            }

            _dbContext.SaveChanges();
        }
    }
}
