using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicServer.Data;
using MusicServer.Services;

namespace MusicServer.Controllers
{
    [ApiController]
    [Route("api/albums")]
    public class AlbumsController : ControllerBase
    {
        #region Fields
        private readonly MusicDbContext _dbContext;
        private readonly ILogger<LibraryController> _logger;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="AlbumsController"/> class.
        /// </summary>
        /// <param name="dbContext">The music database context.</param>
        /// <param name="logger">The logger instance.</param>
        public AlbumsController(MusicDbContext dbContext, ILogger<LibraryController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        #endregion

        #region API Endpoints

        /// <summary>
        /// Retrieves the album details and its tracks for a given album ID.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <returns>An object containing album details and tracks.</returns>
        [HttpGet("{albumId}/tracks")]
        public async Task<IActionResult> GetTracksByAlbum(int albumId)
        {
            var album = await _dbContext.Albums
                .Where(al => al.Id == albumId)
                .Select(al => new
                {
                    albumId = al.Id,
                    albumName = al.Name,
                    albumArtist = _dbContext.Artists
                        .Where(art => art.Id == al.ArtistId)
                        .Select(art => art.Name)
                        .FirstOrDefault(),
                    releaseYear = al.ReleaseYear,
                    genre = al.Genre,
                    coverArtUrl = $"/api/library/artwork/{albumId}",
                    trackCount = _dbContext.Tracks.Count(t => t.AlbumId == al.Id),
                    dateAdded = al.DateAdded
                })
                .FirstOrDefaultAsync();

            if (album == null)
            {
                _logger.LogError("Album not found");
                return StatusCode(404, "Album not found");
            }

            var tracks = await _dbContext.Tracks
                .Where(t => t.AlbumId == albumId || t.AlbumId == albumId)
                .OrderBy(t => t.DiscNumber) // Sort by disc number first
                .ThenBy(t => t.TrackNumber) // Then sort by track number
                .Select(t => new
                {
                    id = t.Id,
                    title = t.Title,
                    trackNumber = t.TrackNumber,
                    discNumber = t.DiscNumber,
                    duration = t.Duration,
                    bitrate = t.Bitrate,
                    format = t.FileFormat,
                    filePath = t.FilePath,
                    fileSize = t.FileSize,
                    liked = t.Liked
                })
                .ToListAsync();

            return Ok(new { album, tracks });
        }

        #endregion
    }
}