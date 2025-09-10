using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
            CreatePlaylistRequest request,
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
            UpdatePlaylistRequest request,
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
    }
}