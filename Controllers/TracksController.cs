using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicServer.Data;
using MusicServer.Services;

namespace MusicServer.Controllers
{
    [ApiController]
    [Route("api/tracks")]
    public class TracksController : ControllerBase
    {
        #region Fields
        private readonly MusicDbContext _dbContext;
        private readonly ILogger<LibraryController> _logger;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="TracksController"/> class.
        /// </summary>
        /// <param name="dbContext">The music database context.</param>
        /// <param name="logger">The logger instance.</param>
        public TracksController(MusicDbContext dbContext, ILogger<LibraryController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        #endregion

        #region API Endpoints
        // GET /api/tracks/{trackId}/metadata
        [HttpGet("{trackId}/metadata")]
        public async Task<IActionResult> GetTrackMetadata(int trackId)
        {
            var track = await _dbContext.Tracks
                .Include(t => t.Album)
                .ThenInclude(a => a.Artist) // Include album and artist info
                .FirstOrDefaultAsync(t => t.Id == trackId);

            if (track == null)
            {
                return NotFound(new { message = "Track not found" });
            }

            return Ok(new
            {
                id = track.Id,
                title = track.Title,
                artist = track.Album.Artist.Name,
                album = track.Album.Name,
                releaseYear = track.Album.ReleaseYear,
                bitrate = track.Bitrate,
                format = track.FileFormat,
                duration = track.Duration,
                trackNumber = track.TrackNumber,
                discNumber = track.DiscNumber,
                filePath = track.FilePath,
                fileSize = track.FileSize
            });
        }

        #endregion

    }
}