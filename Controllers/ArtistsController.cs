using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicServer.Data;
using MusicServer.Services;

namespace MusicServer.Controllers
{
    [ApiController]
    [Route("api/artists")]
    public class ArtistController : ControllerBase
    {
        #region Fields
        private readonly MusicDbContext _dbContext;
        private readonly ILogger<LibraryController> _logger;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ArtistController"/> class.
        /// </summary>
        /// <param name="dbContext">The music database context.</param>
        /// <param name="logger">The logger instance.</param>
        public ArtistController(MusicDbContext dbContext, ILogger<LibraryController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        #endregion

        #region API Endpoints

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
        /// Retrieves a list of albums by artist.
        /// </summary>
        /// <param name="artistId">Artist ID to filter the albums.</param>
        /// <returns>A list of albums.</returns>
        [HttpGet("{artistId}/albums")]
        public async Task<IActionResult> GetAlbumsByArtist(int artistId)
        {
            var query = _dbContext.Albums.AsQueryable();
            var artist = await _dbContext.Artists
                .Include(a => a.Albums)
                .FirstOrDefaultAsync(a => a.Id == artistId);

            if (artist == null)
            {
                _logger.LogError("Artist not found");
                return StatusCode(404, "Artist not found");
            }

            var albums = await query
                    .Select(album => new
                    {
                        albumId = album.Id,
                        albumName = album.Name,
                        albumArtist = album.Artist.Name,
                        artistId = album.ArtistId,
                        releaseYear = album.ReleaseYear,
                        genre = album.Genre,
                        releaseType = album.ReleaseType,
                        coverArtUrl = $"/api/library/artwork/{album.Id}",
                        trackCount = _dbContext.Tracks.Count(t => t.AlbumId == album.Id),
                        dateAdded = album.DateAdded
                    })
                    .Where(album => album.artistId == artistId)
                    .OrderBy(album => album.albumArtist)
                    .ThenByDescending(album => album.releaseYear)
                    .ToListAsync();

            return Ok(new
            {
                artist = artist.Name,
                albums
            });
        }

        #endregion
    }
}