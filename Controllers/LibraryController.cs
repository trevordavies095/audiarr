using Microsoft.AspNetCore.Mvc;
using MusicServer.Services;  // Add this namespace to access LibraryScanner
using MusicServer.Data;
using Microsoft.EntityFrameworkCore;

namespace MusicServer.Controllers
{
    [ApiController]
    [Route("api/library")]
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
                var artists = await _dbContext.Artists
                    .Select(a => new
                    {
                        name = a.Name,
                        id = a.Id,
                        sortName = a.SortName,
                        albumCount = _dbContext.Albums.Count(al => al.ArtistId == a.Id),
                        trackCount = _dbContext.Tracks.Count(t => t.ArtistId == a.Id)
                    })
                    .OrderBy(a => a.sortName)
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
        public async Task<IActionResult> GetAlbums([FromQuery] int? artistId = null)
        {
            try
            {
                var query = _dbContext.Albums.AsQueryable();

                if (artistId.HasValue)
                {
                    query = query.Where(album => album.ArtistId == artistId.Value);
                }

                var albums = await query
                    .Select(album => new
                    {
                        albumId = album.Id,
                        albumName = album.Name,
                        albumArtist = _dbContext.Artists.Where(a => a.Id == album.ArtistId).Select(a => a.Name).FirstOrDefault(),
                        releaseYear = album.ReleaseYear,
                        genre = album.Genre,
                        coverArtUrl = album.CoverArtUrl,
                        trackCount = _dbContext.Tracks.Count(t => t.AlbumId == album.Id)
                    })
                    .OrderBy(a => a.albumArtist)
                    .ThenBy(a => a.releaseYear)
                    .ToListAsync();

                return Ok(albums);
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Error fetching albums for artist {Artist}: {Message}", artistId, ex.Message);
                return StatusCode(500, "An error occurred while fetching albums.");
            }
        }

        [HttpGet("tracks")]
        public async Task<IActionResult> GetTracks([FromQuery] int albumId)
        {
            try
            {
                var album = await _dbContext.Albums
                    .Where(a => a.Id == albumId)
                    .Select(a => new
                    {
                        albumId = a.Id,
                        albumName = a.Name,
                        albumArtist = _dbContext.Artists.Where(art => art.Id == a.ArtistId).Select(art => art.Name).FirstOrDefault(),
                        releaseYear = a.ReleaseYear,
                        genre = a.Genre,
                        coverArtUrl = a.CoverArtUrl,
                        trackCount = _dbContext.Tracks.Count(t => t.AlbumId == a.Id)
                    })
                    .FirstOrDefaultAsync();

                if (album == null)
                    return NotFound("Album not found");

                var tracks = await _dbContext.Tracks
                    .Where(t => t.AlbumId == albumId)
                    .Select(t => new
                    {
                        id = t.Id,
                        trackTitle = t.Title,
                        artist = _dbContext.Artists.Where(a => a.Id == t.ArtistId).Select(a => a.Name).FirstOrDefault(),
                        trackNumber = t.TrackNumber,
                        duration = t.Duration,
                        fileFormat = t.FileFormat,
                        bitrate = t.Bitrate,
                        fileSize = t.FileSize,
                        streamUrl = $"/api/music/stream/{t.Id}"
                    })
                    .OrderBy(t => t.trackNumber)
                    .ToListAsync();

                return Ok(new { album, tracks });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error fetching tracks: {Message}", ex.Message);
                return StatusCode(500, "An error occurred while fetching tracks.");
            }
        }

        [HttpGet("search")]
        public IActionResult SearchLibrary([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query parameter is required.");
            }

            // Normalize query
            query = query.Trim().ToLower();

            // Search for matching artists
            var artists = _dbContext.Artists
                .Where(a => a.Name.ToLower().Contains(query))
                .Select(a => new
                {
                    a.Id,
                    a.Name
                })
                .Take(50)
                .ToList();

            // Search for matching albums
            var albums = _dbContext.Albums
                .Where(al => al.Name.ToLower().Contains(query))
                .Select(al => new
                {
                    al.Id,
                    al.Name,
                    ArtistName = _dbContext.Artists.Where(a => a.Id == al.ArtistId).Select(a => a.Name).FirstOrDefault(),
                    al.ReleaseYear,
                    al.CoverArtUrl
                })
                .Take(50)
                .ToList();

            // Search for matching tracks
            var tracks = _dbContext.Tracks
                .Include(t => t.Album)  // Ensure Album is loaded
                .ThenInclude(a => a.Artist) // Ensure Artist is loaded
                .Where(t => t.Title.ToLower().Contains(query))
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    ArtistName = t.Artist.Name, // Direct reference to Artist
                    AlbumName = t.Album.Name,
                    t.Duration
                })
                .Take(50)
                .ToList();

            return Ok(new { artists, albums, tracks });
        }
    }
}
