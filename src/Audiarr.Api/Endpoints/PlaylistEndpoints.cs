using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Audiarr.Data.Context;
using Audiarr.Core.DTOs;
using Audiarr.Core.DTOs.Requests;
using Audiarr.Core.Entities;

namespace Audiarr.Api.Endpoints;

public static class PlaylistEndpoints
{
    public static void MapPlaylistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v2/playlists")
            .WithTags("Playlists")
            .RequireAuthorization();

        // GET /api/v2/playlists - List user's playlists with pagination
        group.MapGet("/", async (
            ClaimsPrincipal user,
            AudiarrContext db,
            int page = 1,
            int limit = 50,
            bool includePublic = false) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            if (page < 1) page = 1;
            if (limit < 1 || limit > 100) limit = 50;

            var query = db.Playlists
                .Include(p => p.User)
                .Where(p => p.UserId == userId || (includePublic && p.IsPublic))
                .OrderByDescending(p => p.LastModified);

            var total = await query.CountAsync();

            var playlists = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(p => new PlaylistDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    UserId = p.UserId,
                    Username = p.User.Username,
                    IsPublic = p.IsPublic,
                    ImagePath = p.ImagePath,
                    TrackCount = p.TrackCount,
                    TotalDuration = p.TotalDuration,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    LastModified = p.LastModified,
                    PlayCount = p.PlayCount
                })
                .ToListAsync();

            return Results.Ok(new
            {
                data = playlists,
                page,
                limit,
                total,
                totalPages = (int)Math.Ceiling((double)total / limit)
            });
        })
        .WithName("GetPlaylists")
        .WithOpenApi()
        .WithSummary("Get user's playlists")
        .WithDescription("Returns a paginated list of user's playlists, optionally including public playlists from other users");

        // GET /api/v2/playlists/{id} - Get playlist details with tracks
        group.MapGet("/{id}", async (
            string id,
            ClaimsPrincipal user,
            AudiarrContext db) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var playlist = await db.Playlists
                .Include(p => p.User)
                .Include(p => p.PlaylistTracks)
                    .ThenInclude(pt => pt.Track)
                        .ThenInclude(t => t.Artist)
                .Include(p => p.PlaylistTracks)
                    .ThenInclude(pt => pt.Track)
                        .ThenInclude(t => t.Album)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (playlist == null)
                return Results.NotFound(new { error = "Playlist not found" });

            // Check authorization - user can only view their own playlists or public playlists
            if (playlist.UserId != userId && !playlist.IsPublic)
                return Results.Forbid();

            var playlistDetails = new PlaylistDetailsDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Description = playlist.Description,
                UserId = playlist.UserId,
                Username = playlist.User.Username,
                IsPublic = playlist.IsPublic,
                ImagePath = playlist.ImagePath,
                TrackCount = playlist.TrackCount,
                TotalDuration = playlist.TotalDuration,
                CreatedAt = playlist.CreatedAt,
                UpdatedAt = playlist.UpdatedAt,
                LastModified = playlist.LastModified,
                PlayCount = playlist.PlayCount,
                Tracks = playlist.PlaylistTracks
                    .OrderBy(pt => pt.Position)
                    .ThenBy(pt => pt.PositionFloat)
                    .Select(pt => new PlaylistTrackDto
                    {
                        TrackId = pt.Track.Id,
                        Title = pt.Track.Title,
                        ArtistId = pt.Track.ArtistId,
                        ArtistName = pt.Track.Artist.Name,
                        AlbumId = pt.Track.AlbumId,
                        AlbumTitle = pt.Track.Album.Title,
                        TrackNumber = pt.Track.TrackNumber,
                        DiscNumber = pt.Track.DiscNumber,
                        DurationMs = pt.Track.DurationMs,
                        Genre = pt.Track.Genre,
                        Year = pt.Track.Year,
                        FilePath = pt.Track.FilePath,
                        Position = pt.Position,
                        PositionFloat = pt.PositionFloat,
                        AddedAt = pt.AddedAt,
                        AddedBy = pt.AddedBy
                    })
                    .ToList()
            };

            return Results.Ok(playlistDetails);
        })
        .WithName("GetPlaylistById")
        .WithOpenApi()
        .WithSummary("Get playlist by ID")
        .WithDescription("Returns a playlist with all tracks included");

        // POST /api/v2/playlists - Create new playlist
        group.MapPost("/", async (
            [FromBody] CreatePlaylistRequest request,
            ClaimsPrincipal user,
            AudiarrContext db) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = user.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var playlist = new Playlist
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                UserId = userId,
                IsPublic = request.IsPublic,
                LastModified = DateTime.UtcNow,
                TrackCount = 0,
                PlayCount = 0
            };

            db.Playlists.Add(playlist);

            // Add initial tracks if provided
            if (request.InitialTrackIds != null && request.InitialTrackIds.Count > 0)
            {
                var tracks = await db.Tracks
                    .Where(t => request.InitialTrackIds.Contains(t.Id))
                    .ToListAsync();

                decimal position = 0;
                foreach (var trackId in request.InitialTrackIds)
                {
                    if (tracks.Any(t => t.Id == trackId))
                    {
                        var playlistTrack = new PlaylistTrack
                        {
                            PlaylistId = playlist.Id,
                            TrackId = trackId,
                            Position = (int)position,
                            PositionFloat = position,
                            AddedAt = DateTime.UtcNow,
                            AddedBy = username
                        };
                        db.PlaylistTracks.Add(playlistTrack);
                        position++;
                    }
                }

                playlist.TrackCount = (int)position;
                
                // Calculate total duration
                var totalMs = tracks.Sum(t => t.DurationMs);
                if (totalMs > 0)
                    playlist.TotalDuration = TimeSpan.FromMilliseconds(totalMs);
            }

            await db.SaveChangesAsync();

            // Load the user for the response
            await db.Entry(playlist).Reference(p => p.User).LoadAsync();

            var response = new PlaylistDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Description = playlist.Description,
                UserId = playlist.UserId,
                Username = playlist.User.Username,
                IsPublic = playlist.IsPublic,
                ImagePath = playlist.ImagePath,
                TrackCount = playlist.TrackCount,
                TotalDuration = playlist.TotalDuration,
                CreatedAt = playlist.CreatedAt,
                UpdatedAt = playlist.UpdatedAt,
                LastModified = playlist.LastModified,
                PlayCount = playlist.PlayCount
            };

            return Results.Created($"/api/v2/playlists/{playlist.Id}", response);
        })
        .WithName("CreatePlaylist")
        .WithOpenApi()
        .WithSummary("Create new playlist")
        .WithDescription("Creates a new playlist, optionally with initial tracks");

        // PUT /api/v2/playlists/{id} - Update playlist metadata
        group.MapPut("/{id}", async (
            string id,
            [FromBody] UpdatePlaylistRequest request,
            ClaimsPrincipal user,
            AudiarrContext db) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var playlist = await db.Playlists
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (playlist == null)
                return Results.NotFound(new { error = "Playlist not found" });

            // Check authorization - user can only update their own playlists
            if (playlist.UserId != userId)
                return Results.Forbid();

            playlist.Name = request.Name;
            playlist.Description = request.Description;
            playlist.IsPublic = request.IsPublic;
            playlist.LastModified = DateTime.UtcNow;
            playlist.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            var response = new PlaylistDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Description = playlist.Description,
                UserId = playlist.UserId,
                Username = playlist.User.Username,
                IsPublic = playlist.IsPublic,
                ImagePath = playlist.ImagePath,
                TrackCount = playlist.TrackCount,
                TotalDuration = playlist.TotalDuration,
                CreatedAt = playlist.CreatedAt,
                UpdatedAt = playlist.UpdatedAt,
                LastModified = playlist.LastModified,
                PlayCount = playlist.PlayCount
            };

            return Results.Ok(response);
        })
        .WithName("UpdatePlaylist")
        .WithOpenApi()
        .WithSummary("Update playlist metadata")
        .WithDescription("Updates playlist name, description, and visibility settings");

        // DELETE /api/v2/playlists/{id} - Delete playlist
        group.MapDelete("/{id}", async (
            string id,
            ClaimsPrincipal user,
            AudiarrContext db) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var playlist = await db.Playlists
                .FirstOrDefaultAsync(p => p.Id == id);

            if (playlist == null)
                return Results.NotFound(new { error = "Playlist not found" });

            // Check authorization - user can only delete their own playlists
            if (playlist.UserId != userId)
                return Results.Forbid();

            // Remove all playlist tracks first
            var playlistTracks = await db.PlaylistTracks
                .Where(pt => pt.PlaylistId == id)
                .ToListAsync();
            
            db.PlaylistTracks.RemoveRange(playlistTracks);
            db.Playlists.Remove(playlist);
            
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Playlist deleted successfully" });
        })
        .WithName("DeletePlaylist")
        .WithOpenApi()
        .WithSummary("Delete playlist")
        .WithDescription("Deletes a playlist and all its track associations");

        // GET /api/v2/playlists/public - Get all public playlists
        group.MapGet("/public", async (
            AudiarrContext db,
            int page = 1,
            int limit = 50) =>
        {
            if (page < 1) page = 1;
            if (limit < 1 || limit > 100) limit = 50;

            var query = db.Playlists
                .Include(p => p.User)
                .Where(p => p.IsPublic)
                .OrderByDescending(p => p.PlayCount)
                .ThenByDescending(p => p.LastModified);

            var total = await query.CountAsync();

            var playlists = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(p => new PlaylistDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    UserId = p.UserId,
                    Username = p.User.Username,
                    IsPublic = p.IsPublic,
                    ImagePath = p.ImagePath,
                    TrackCount = p.TrackCount,
                    TotalDuration = p.TotalDuration,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    LastModified = p.LastModified,
                    PlayCount = p.PlayCount
                })
                .ToListAsync();

            return Results.Ok(new
            {
                data = playlists,
                page,
                limit,
                total,
                totalPages = (int)Math.Ceiling((double)total / limit)
            });
        })
        .WithName("GetPublicPlaylists")
        .WithOpenApi()
        .WithSummary("Get public playlists")
        .WithDescription("Returns a paginated list of all public playlists");

        // POST /api/v2/playlists/{id}/tracks - Add tracks to playlist
        group.MapPost("/{id}/tracks", async (
            string id,
            [FromBody] AddTracksRequest request,
            ClaimsPrincipal user,
            AudiarrContext db) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = user.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var playlist = await db.Playlists
                .Include(p => p.PlaylistTracks)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (playlist == null)
                return Results.NotFound(new { error = "Playlist not found" });

            // Check authorization - user can only modify their own playlists
            if (playlist.UserId != userId)
                return Results.Forbid();

            // Verify tracks exist
            var tracks = await db.Tracks
                .Where(t => request.TrackIds.Contains(t.Id))
                .ToListAsync();

            if (!tracks.Any())
                return Results.BadRequest(new { error = "No valid tracks found" });

            // Get existing track IDs to prevent duplicates
            var existingTrackIds = playlist.PlaylistTracks.Select(pt => pt.TrackId).ToHashSet();

            // Determine starting position
            decimal startPosition;
            if (request.Position.HasValue && request.Position.Value >= 0)
            {
                // Insert at specific position
                if (request.Position.Value == 0)
                {
                    // Insert at beginning
                    var firstTrack = playlist.PlaylistTracks.OrderBy(pt => pt.PositionFloat).FirstOrDefault();
                    startPosition = firstTrack != null ? firstTrack.PositionFloat - 1 : 0;
                }
                else
                {
                    // Insert after specified position
                    var tracksOrdered = playlist.PlaylistTracks.OrderBy(pt => pt.PositionFloat).ToList();
                    if (request.Position.Value >= tracksOrdered.Count)
                    {
                        // Append to end
                        var lastTrack = tracksOrdered.LastOrDefault();
                        startPosition = lastTrack != null ? lastTrack.PositionFloat + 1 : 0;
                    }
                    else
                    {
                        // Insert between tracks
                        var prevTrack = tracksOrdered[request.Position.Value - 1];
                        var nextTrack = tracksOrdered[request.Position.Value];
                        startPosition = (prevTrack.PositionFloat + nextTrack.PositionFloat) / 2;
                    }
                }
            }
            else
            {
                // Append to end by default
                var lastTrack = playlist.PlaylistTracks.OrderByDescending(pt => pt.PositionFloat).FirstOrDefault();
                startPosition = lastTrack != null ? lastTrack.PositionFloat + 1 : 0;
            }

            // Add new tracks
            var addedCount = 0;
            var totalDurationMs = 0;
            foreach (var trackId in request.TrackIds)
            {
                if (!existingTrackIds.Contains(trackId) && tracks.Any(t => t.Id == trackId))
                {
                    var track = tracks.First(t => t.Id == trackId);
                    var playlistTrack = new PlaylistTrack
                    {
                        PlaylistId = playlist.Id,
                        TrackId = trackId,
                        Position = 0, // Will be recalculated
                        PositionFloat = startPosition + addedCount,
                        AddedAt = DateTime.UtcNow,
                        AddedBy = username
                    };
                    db.PlaylistTracks.Add(playlistTrack);
                    totalDurationMs += track.DurationMs;
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                // Update playlist metadata
                playlist.TrackCount += addedCount;
                playlist.LastModified = DateTime.UtcNow;
                playlist.UpdatedAt = DateTime.UtcNow;

                // Update total duration
                if (totalDurationMs > 0)
                {
                    var currentDuration = playlist.TotalDuration?.TotalMilliseconds ?? 0;
                    playlist.TotalDuration = TimeSpan.FromMilliseconds(currentDuration + totalDurationMs);
                }

                // Recalculate integer positions
                var allTracks = await db.PlaylistTracks
                    .Where(pt => pt.PlaylistId == id)
                    .OrderBy(pt => pt.PositionFloat)
                    .ToListAsync();

                for (int i = 0; i < allTracks.Count; i++)
                {
                    allTracks[i].Position = i;
                }

                await db.SaveChangesAsync();
            }

            return Results.Ok(new
            {
                message = $"Added {addedCount} track(s) to playlist",
                addedCount,
                totalTracks = playlist.TrackCount
            });
        })
        .WithName("AddTracksToPlaylist")
        .WithOpenApi()
        .WithSummary("Add tracks to playlist")
        .WithDescription("Adds one or more tracks to a playlist at the specified position");

        // DELETE /api/v2/playlists/{id}/tracks - Remove tracks from playlist
        group.MapDelete("/{id}/tracks", async (
            string id,
            [FromBody] RemoveTracksRequest request,
            ClaimsPrincipal user,
            AudiarrContext db) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var playlist = await db.Playlists
                .Include(p => p.PlaylistTracks)
                    .ThenInclude(pt => pt.Track)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (playlist == null)
                return Results.NotFound(new { error = "Playlist not found" });

            // Check authorization - user can only modify their own playlists
            if (playlist.UserId != userId)
                return Results.Forbid();

            // Find tracks to remove
            var tracksToRemove = playlist.PlaylistTracks
                .Where(pt => request.TrackIds.Contains(pt.TrackId))
                .ToList();

            if (!tracksToRemove.Any())
                return Results.BadRequest(new { error = "No matching tracks found in playlist" });

            // Calculate total duration to subtract
            var totalDurationMs = tracksToRemove.Sum(pt => pt.Track.DurationMs);

            // Remove tracks
            db.PlaylistTracks.RemoveRange(tracksToRemove);

            // Update playlist metadata
            playlist.TrackCount -= tracksToRemove.Count;
            playlist.LastModified = DateTime.UtcNow;
            playlist.UpdatedAt = DateTime.UtcNow;

            // Update total duration
            if (totalDurationMs > 0 && playlist.TotalDuration.HasValue)
            {
                var newDurationMs = Math.Max(0, playlist.TotalDuration.Value.TotalMilliseconds - totalDurationMs);
                playlist.TotalDuration = newDurationMs > 0 ? TimeSpan.FromMilliseconds(newDurationMs) : null;
            }

            // Recalculate positions for remaining tracks
            var remainingTracks = playlist.PlaylistTracks
                .Except(tracksToRemove)
                .OrderBy(pt => pt.PositionFloat)
                .ToList();

            for (int i = 0; i < remainingTracks.Count; i++)
            {
                remainingTracks[i].Position = i;
                remainingTracks[i].PositionFloat = i;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = $"Removed {tracksToRemove.Count} track(s) from playlist",
                removedCount = tracksToRemove.Count,
                remainingTracks = playlist.TrackCount
            });
        })
        .WithName("RemoveTracksFromPlaylist")
        .WithOpenApi()
        .WithSummary("Remove tracks from playlist")
        .WithDescription("Removes one or more tracks from a playlist");

        // PUT /api/v2/playlists/{id}/tracks/reorder - Reorder tracks in playlist
        group.MapPut("/{id}/tracks/reorder", async (
            string id,
            [FromBody] ReorderTracksRequest request,
            ClaimsPrincipal user,
            AudiarrContext db) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var playlist = await db.Playlists
                .Include(p => p.PlaylistTracks)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (playlist == null)
                return Results.NotFound(new { error = "Playlist not found" });

            // Check authorization - user can only modify their own playlists
            if (playlist.UserId != userId)
                return Results.Forbid();

            // Validate all track IDs exist in the playlist
            var playlistTrackIds = playlist.PlaylistTracks.Select(pt => pt.TrackId).ToHashSet();
            var requestTrackIds = request.Tracks.Select(t => t.TrackId).ToHashSet();

            if (!requestTrackIds.IsSubsetOf(playlistTrackIds))
                return Results.BadRequest(new { error = "One or more tracks not found in playlist" });

            // Update positions for specified tracks
            var tracksToUpdate = playlist.PlaylistTracks
                .Where(pt => requestTrackIds.Contains(pt.TrackId))
                .ToDictionary(pt => pt.TrackId);

            foreach (var reorderItem in request.Tracks)
            {
                if (tracksToUpdate.TryGetValue(reorderItem.TrackId, out var track))
                {
                    track.PositionFloat = reorderItem.NewPosition;
                }
            }

            // Recalculate integer positions based on new float positions
            var allTracksOrdered = playlist.PlaylistTracks
                .OrderBy(pt => pt.PositionFloat)
                .ToList();

            for (int i = 0; i < allTracksOrdered.Count; i++)
            {
                allTracksOrdered[i].Position = i;
            }

            // Update playlist metadata
            playlist.LastModified = DateTime.UtcNow;
            playlist.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Playlist tracks reordered successfully",
                reorderedCount = request.Tracks.Count
            });
        })
        .WithName("ReorderPlaylistTracks")
        .WithOpenApi()
        .WithSummary("Reorder tracks in playlist")
        .WithDescription("Reorders tracks within a playlist using decimal positioning for conflict-free updates");
    }
}