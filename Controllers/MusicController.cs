using Microsoft.AspNetCore.Mvc;
using MusicServer.Data;

namespace MusicServer.Controllers
{
    /// <summary>
    /// Controller responsible for streaming music tracks.
    /// </summary>
    [ApiController]
    [Route("api/music")]
    public class MusicController : ControllerBase
    {
        #region Fields

        /// <summary>
        /// The database context used to access track data.
        /// </summary>
        private readonly MusicDbContext _dbContext;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MusicController"/> class.
        /// </summary>
        /// <param name="dbContext">The music database context.</param>
        public MusicController(MusicDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        #endregion

        #region API Endpoints

        /// <summary>
        /// Streams the audio file for a given track ID.
        /// </summary>
        /// <param name="id">The track identifier.</param>
        /// <returns>A file stream of the audio file with range processing enabled.</returns>
        [HttpGet("stream/{id}")]
        public IActionResult StreamTrack(int id)
        {
            // Retrieve the track from the database.
            var track = _dbContext.Tracks.Find(id);

            // Return 404 if the track is not found or the file does not exist.
            if (track == null || !System.IO.File.Exists(track.FilePath))
            {
                return NotFound("Track not found.");
            }

            // Open a file stream for the track file.
            var fileStream = new FileStream(track.FilePath, FileMode.Open, FileAccess.Read);

            // Return the file stream with MIME type set for MP3 and enable range processing.
            return File(fileStream, "audio/mpeg", enableRangeProcessing: true);
        }

        #endregion
    }
}
