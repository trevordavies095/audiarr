using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicServer.Data;
using MusicServer.Services;

namespace MusicServer.Controllers
{
    /// <summary>
    /// Controller for handling library operations such as scanning, retrieving artists, albums, tracks,
    /// performing searches, and retrieving album artwork.
    /// </summary>
    [ApiController]
    [Route("api/library")]
    public class LibraryController : ControllerBase
    {
        #region Fields

        private readonly MusicDbContext _dbContext;
        private readonly LibraryScanner _libraryScanner;
        private readonly ILogger<LibraryController> _logger;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="LibraryController"/> class.
        /// </summary>
        /// <param name="dbContext">The music database context.</param>
        /// <param name="libraryScanner">The library scanning service.</param>
        /// <param name="logger">The logger instance.</param>
        public LibraryController(MusicDbContext dbContext, LibraryScanner libraryScanner, ILogger<LibraryController> logger)
        {
            _dbContext = dbContext;
            _libraryScanner = libraryScanner;
            _logger = logger;
        }

        #endregion

        #region API Endpoints

        /// <summary>
        /// Initiates a scan of the music library.
        /// </summary>
        /// <returns>A success or error response.</returns>
        [HttpPost("scan")]
        public IActionResult ScanLibrary()
        {
            try
            {
                // Trigger the library scanning process.
                _libraryScanner.ScanLibrary();
                return Ok("Scan started successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error scanning library: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a list of artists along with album and track counts.
        /// </summary>
        /// <returns>A list of artists.</returns>
        [HttpGet("artists")]
        public async Task<IActionResult> GetArtists()
        {
            try
            {
                // Query artists with their associated album and track counts.
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

        /// <summary>
        /// Retrieves a list of albums, optionally filtered by artist.
        /// </summary>
        /// <param name="artistId">Optional artist ID to filter the albums.</param>
        /// <returns>A list of albums.</returns>
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
                        albumArtist = album.Artist.Name,
                        releaseYear = album.ReleaseYear,
                        genre = album.Genre,
                        coverArtUrl = $"/api/library/artwork/{album.Id}",
                        trackCount = _dbContext.Tracks.Count(t => t.AlbumId == album.Id),
                        dateAdded = album.DateAdded
                    })
                    .OrderBy(a => a.albumArtist)
                    .ThenByDescending(a => a.releaseYear)
                    .ToListAsync();

                return Ok(albums);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error fetching albums for artist {Artist}: {Message}", artistId, ex.Message);
                return StatusCode(500, "An error occurred while fetching albums.");
            }
        }

        /// <summary>
        /// Retrieves the album details and its tracks for a given album ID.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <returns>An object containing album details and tracks.</returns>
        [HttpGet("tracks")]
        public async Task<IActionResult> GetTracks([FromQuery] int albumId)
        {
            try
            {
                // Retrieve album details.
                var album = await _dbContext.Albums
                    .Where(a => a.Id == albumId)
                    .Select(a => new
                    {
                        albumId = a.Id,
                        albumName = a.Name,
                        albumArtist = _dbContext.Artists
                            .Where(art => art.Id == a.ArtistId)
                            .Select(art => art.Name)
                            .FirstOrDefault(),
                        releaseYear = a.ReleaseYear,
                        genre = a.Genre,
                        coverArtUrl = $"/api/library/artwork/{albumId}",
                        trackCount = _dbContext.Tracks.Count(t => t.AlbumId == a.Id),
                        dateAdded = a.DateAdded
                    })
                    .FirstOrDefaultAsync();

                if (album == null)
                    return NotFound("Album not found");

                // Retrieve tracks associated with the album.
                var tracks = await _dbContext.Tracks
                    .Where(t => t.AlbumId == albumId)
                    .Select(t => new
                    {
                        id = t.Id,
                        trackTitle = t.Title,
                        artist = _dbContext.Artists
                            .Where(a => a.Id == t.ArtistId)
                            .Select(a => a.Name)
                            .FirstOrDefault(),
                        trackNumber = t.TrackNumber,
                        discNumber = t.DiscNumber,
                        duration = t.Duration,
                        fileFormat = t.FileFormat,
                        bitrate = t.Bitrate,
                        fileSize = t.FileSize,
                        streamUrl = $"/api/music/stream/{t.Id}"
                    })
                    .OrderBy(t => t.discNumber)
                    .ThenBy(t => t.trackNumber)
                    .ToListAsync();

                return Ok(new { album, tracks });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error fetching tracks: {Message}", ex.Message);
                return StatusCode(500, "An error occurred while fetching tracks.");
            }
        }

        /// <summary>
        /// Searches the music library for matching artists, albums, and tracks.
        /// </summary>
        /// <param name="query">The search query string.</param>
        /// <returns>An object containing the search results.</returns>
        [HttpGet("search")]
        public IActionResult SearchLibrary([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query parameter is required.");
            }

            // Normalize the search query.
            query = query.Trim().ToLower();

            // Search for matching artists.
            var artists = _dbContext.Artists
                .Where(a => a.Name.ToLower().Contains(query))
                .Select(a => new
                {
                    a.Id,
                    a.Name
                })
                .Take(50)
                .ToList();

            // Search for matching albums.
            var albums = _dbContext.Albums
                .Where(al => al.Name.ToLower().Contains(query))
                .Select(al => new
                {
                    al.Id,
                    al.Name,
                    ArtistName = _dbContext.Artists
                        .Where(a => a.Id == al.ArtistId)
                        .Select(a => a.Name)
                        .FirstOrDefault(),
                    al.ReleaseYear,
                    al.CoverArtUrl
                })
                .Take(50)
                .ToList();

            // Search for matching tracks.
            var tracks = _dbContext.Tracks
                .Include(t => t.Album)
                    .ThenInclude(a => a.Artist)
                .Where(t => t.Title.ToLower().Contains(query))
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    ArtistName = t.Artist.Name,
                    AlbumName = t.Album.Name,
                    t.Duration
                })
                .Take(50)
                .ToList();

            return Ok(new { artists, albums, tracks });
        }

        /// <summary>
        /// Retrieves the artwork for a specified album.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <returns>The album artwork as an image file.</returns>
        [HttpGet("artwork/{albumId}")]
        public IActionResult GetAlbumArtwork(int albumId)
        {
            var album = _dbContext.Albums.FirstOrDefault(a => a.Id == albumId);
            if (album == null || string.IsNullOrEmpty(album.CoverArtUrl))
            {
                return NotFound("Cover art not found.");
            }

            if (!System.IO.File.Exists(album.CoverArtUrl))
            {
                return NotFound("Cover art file does not exist.");
            }

            var imageStream = new FileStream(album.CoverArtUrl, FileMode.Open, FileAccess.Read);
            return File(imageStream, "image/jpeg"); // Adjust MIME type as needed.
        }

        /// <summary>
        /// Retrieves the most recently added albums.
        /// </summary>
        /// <returns>A list of recently added albums.</returns>
        [HttpGet("recently-added")]
        public IActionResult GetRecentlyAddedAlbums()
        {
            var albums = _dbContext.Albums
                .OrderByDescending(a => a.DateAdded) // Newest first.
                .Take(50) // Limit to 50 albums.
                .Select(a => new
                {
                    albumId = a.Id,
                    albumName = a.Name,
                    albumArtist = a.Artist.Name,
                    releaseYear = a.ReleaseYear,
                    genre = a.Genre,
                    coverArtUrl = a.CoverArtUrl,
                    trackCount = _dbContext.Tracks.Count(t => t.AlbumId == a.Id),
                    dateAdded = a.DateAdded
                })
                .ToList();

            return Ok(albums);
        }

        #endregion
    }
}
