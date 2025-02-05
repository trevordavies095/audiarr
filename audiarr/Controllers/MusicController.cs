using Microsoft.AspNetCore.Mvc;
using MusicServer.Data;

namespace MusicServer.Controllers
{
    [ApiController]
    [Route("api/music")]
    public class MusicController : ControllerBase
    {
        private readonly MusicDbContext _dbContext;

        public MusicController(MusicDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("stream/{id}")]
        public IActionResult StreamTrack(int id)
        {
            var track = _dbContext.MusicTracks.Find(id);

            if (track == null || !System.IO.File.Exists(track.FilePath))
            {
                return NotFound("Track not found.");
            }

            var fileStream = new FileStream(track.FilePath, FileMode.Open, FileAccess.Read);
            var response = File(fileStream, "audio/mpeg", enableRangeProcessing: true);
            return response;
        }
    }
}
