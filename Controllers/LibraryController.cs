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

        [HttpGet("artists")]
        public async Task<IActionResult> GetArtists()
        {
            try
            {
                // Query distinct artist names, sort ascending.
                var artists = await _dbContext.MusicTracks
                    .GroupBy(track => track.AlbumArtist) // Group by artist
                    .Select(group => new
                    {
                        name = group.Key,
                        albumCount = group.Select(track => track.AlbumName).Distinct().Count(),
                        trackCount = group.Count()
                    })
                    .OrderBy(artist => artist.name) // Sort alphabetically
                    .ToListAsync();

                return Ok(artists);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error fetching artists: {Message}", ex.Message);
                return StatusCode(500, "An error occurred while fetching artists.");
            }
        }

        [HttpGet("albums")]
        public async Task<IActionResult> GetAlbums([FromQuery] string? artist = null)
        {
            try
            {
                var query = _dbContext.MusicTracks.AsQueryable();

                // Apply filter if an artist parameter is provided
                if (!string.IsNullOrEmpty(artist))
                {
                    query = query.Where(track => track.AlbumArtist.ToLower().Trim() == artist.ToLower().Trim());
                }

                var albums = await query
                    .GroupBy(track => new { track.AlbumName, track.AlbumArtist, track.ReleaseYear })
                    .Select(group => new
                    {
                        albumName = group.Key.AlbumName,
                        artist = group.Key.AlbumArtist,
                        releaseYear = group.Key.ReleaseYear,
                        trackCount = group.Count()
                    })
                    .OrderBy(album => album.artist)  // Sort by artist name
                    .ThenBy(album => album.releaseYear) // Sort by release year
                    .ThenBy(album => album.albumName) // Sort by album name
                    .ToListAsync();

                return Ok(albums);
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Error fetching albums for artist {Artist}: {Message}", artist, ex.Message);
                return StatusCode(500, "An error occurred while fetching albums.");
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
