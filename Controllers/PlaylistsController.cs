using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicServer.Data;
using MusicServer.Services;

namespace MusicServer.Controllers
{
    [ApiController]
    [Route("api/playlists")]
    public class PlaylistsController : ControllerBase
    {
        #region Fields
        private readonly MusicDbContext _dbContext;
        private readonly ILogger<LibraryController> _logger;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistsController"/> class.
        /// </summary>
        /// <param name="dbContext">The music database context.</param>
        /// <param name="logger">The logger instance.</param>
        public PlaylistsController(MusicDbContext dbContext, ILogger<LibraryController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        #endregion

        #region API Endpoints
        // POST /api/playlists/{name}
        [HttpPost("{name}")]
        public async Task<IActionResult> CreatePlaylist(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { message = "Playlist name is required" });
            }

            var playlist = new Playlist
            {
                Name = name,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow
            };

            _dbContext.Playlists.Add(playlist);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPlaylistById), new { playlistId = playlist.Id }, new
            {
                playlist.Id,
                playlist.Name,
                playlist.DateCreated,
                playlist.DateModified
            });
        }


        // GET /api/playlists/{playlistId}
        [HttpGet("{playlistId}")]
        public async Task<IActionResult> GetPlaylistById(int playlistId)
        {
            var playlist = await _dbContext.Playlists
                .Where(p => p.Id == playlistId)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.DateCreated,
                    p.DateModified
                })
                .FirstOrDefaultAsync();

            if (playlist == null)
            {
                return NotFound(new { message = "Playlist not found" });
            }

            return Ok(playlist);
        }

        // GET /api/playlists
        [HttpGet]
        public async Task<IActionResult> GetAllPlaylists()
        {
            var playlists = await _dbContext.Playlists
                .OrderByDescending(p => p.DateModified)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.DateCreated,
                    p.DateModified
                })
                .ToListAsync();

            return Ok(playlists);
        }

        // POST /api/playlists/{playlistId}/tracks/{trackId}
        [HttpPost("{playlistId}/tracks/{trackId}")]
        public async Task<IActionResult> AddTrackToPlaylist(int playlistId, int trackId)
        {
            var playlist = await _dbContext.Playlists.FindAsync(playlistId);
            if (playlist == null)
            {
                return NotFound(new { message = "Playlist not found" });
            }

            var track = await _dbContext.Tracks.FindAsync(trackId);
            if (track == null)
            {
                return NotFound(new { message = "Track not found" });
            }

            // Check if track already exists in playlist
            var existingEntry = await _dbContext.PlaylistTracks
                .AnyAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId);

            if (existingEntry)
            {
                return Conflict(new { message = "Track already exists in the playlist" });
            }

            _dbContext.PlaylistTracks.Add(new PlaylistTrack
            {
                PlaylistId = playlistId,
                TrackId = trackId,
                AddedAt = DateTime.UtcNow
            });

            playlist.DateModified = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Track added to playlist" });
        }

        // GET /api/playlists/{playlistId}/tracks
        [HttpGet("{playlistId}/tracks")]
        public async Task<IActionResult> GetTracksInPlaylist(int playlistId)
        {
            var playlist = await _dbContext.Playlists
                .Include(p => p.PlaylistTracks)
                    .ThenInclude(pt => pt.Track)
                        .ThenInclude(t => t.Album) // Ensure Album is loaded
                .Include(p => p.PlaylistTracks)
                    .ThenInclude(pt => pt.Track)
                        .ThenInclude(t => t.Artist) // Ensure Artist is loaded
                .FirstOrDefaultAsync(p => p.Id == playlistId);

            if (playlist == null)
            {
                return NotFound(new { message = "Playlist not found" });
            }

            var tracks = playlist.PlaylistTracks
                .Where(pt => pt.Track != null) // Ensure Track exists
                .Select(pt => new
                {
                    id = pt.Track.Id,
                    title = pt.Track.Title,
                    album = pt.Track.Album != null ? pt.Track.Album.Name : "Unknown Album", // Use default value if null
                    artist = pt.Track.Artist != null ? pt.Track.Artist.Name : "Unknown Artist", // Use default value if null
                    duration = pt.Track.Duration,
                    addedAt = pt.AddedAt
                })
                .ToList();

            return Ok(new
            {
                playlist = playlist.Name,
                tracks
            });
        }

        // DELETE /api/playlists/{playlistId}/tracks/{trackId}
        [HttpDelete("{playlistId}/tracks/{trackId}")]
        public async Task<IActionResult> RemoveTrackFromPlaylist(int playlistId, int trackId)
        {
            var playlistTrack = await _dbContext.PlaylistTracks
                .FirstOrDefaultAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId);

            if (playlistTrack == null)
            {
                return NotFound(new { message = "Track not found in playlist" });
            }

            _dbContext.PlaylistTracks.Remove(playlistTrack);

            var playlist = await _dbContext.Playlists.FindAsync(playlistId);
            if (playlist != null)
            {
                playlist.DateModified = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Track removed from playlist" });
        }

        // PUT /api/playlists/{playlistId}/{newName}
        [HttpPut("{playlistId}/{newName}")]
        public async Task<IActionResult> RenamePlaylist(int playlistId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                return BadRequest(new { message = "New playlist name is required" });
            }

            var playlist = await _dbContext.Playlists.FindAsync(playlistId);
            if (playlist == null)
            {
                return NotFound(new { message = "Playlist not found" });
            }

            playlist.Name = newName;
            playlist.DateModified = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Playlist renamed successfully"});
        }

        // DELETE /api/playlists/{playlistId}
        [HttpDelete("{playlistId}")]
        public async Task<IActionResult> DeletePlaylist(int playlistId)
        {
            var playlist = await _dbContext.Playlists
                .Include(p => p.PlaylistTracks) // Load associated tracks
                .FirstOrDefaultAsync(p => p.Id == playlistId);

            if (playlist == null)
            {
                return NotFound(new { message = "Playlist not found" });
            }

            // Remove all tracks from the playlist
            _dbContext.PlaylistTracks.RemoveRange(playlist.PlaylistTracks);

            // Delete the playlist itself
            _dbContext.Playlists.Remove(playlist);

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Playlist deleted successfully" });
        }








        #endregion

    }
}