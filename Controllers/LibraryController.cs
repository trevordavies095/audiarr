using Microsoft.AspNetCore.Mvc;
using MusicServer.Services;  // Add this namespace to access LibraryScanner
using MusicServer.Data;
using Microsoft.EntityFrameworkCore;

namespace MusicServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LibraryController : ControllerBase
    {
        private readonly MusicDbContext _dbContext;
        private readonly LibraryScanner _libraryScanner;
        private readonly ILogger<LibraryController> _logger;

        // Inject the LibraryScanner into the controller's constructor
        public LibraryController(MusicDbContext dbContext, LibraryScanner libraryScanner, ILogger<LibraryController> logger)
        {
            _dbContext = dbContext;
            _libraryScanner = libraryScanner;
            _logger = logger;
        }

        [HttpPost("scan")]
        public IActionResult ScanLibrary()
        {
            try
            {
                // Call the scanning method on the LibraryScanner
                _libraryScanner.ScanLibrary();
                return Ok("Scan started successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error scanning library: {ex.Message}");
            }
        }

        [HttpGet("tracks")]
        public async Task<IActionResult> GetTracks([FromQuery] int page = 0, [FromQuery] int pageSize = 1000)
        {
            try
            {
                // Sort the tracks using your desired criteria (e.g., artist, releaseYear, trackNumber)
                var tracks = await _dbContext.MusicTracks
                    .OrderBy(t => t.Artist)
                    .ThenBy(t => t.ReleaseYear)
                    .ThenBy(t => t.TrackNumber)
                    .Skip(page * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
                
                // Optionally, return the total count in headers or in a wrapper object
                return Ok(tracks);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error fetching tracks: {Message}", ex.Message);
                return StatusCode(500, "An error occurred while fetching tracks.");
            }
        }

    }
}
