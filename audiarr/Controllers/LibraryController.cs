using Microsoft.AspNetCore.Mvc;
using MusicServer.Services;  // Add this namespace to access LibraryScanner
using MusicServer.Data;

namespace MusicServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LibraryController : ControllerBase
    {
        private readonly MusicDbContext _dbContext;
        private readonly LibraryScanner _libraryScanner;

        // Inject the LibraryScanner into the controller's constructor
        public LibraryController(MusicDbContext dbContext, LibraryScanner libraryScanner)
        {
            _dbContext = dbContext;
            _libraryScanner = libraryScanner;
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
    }
}
