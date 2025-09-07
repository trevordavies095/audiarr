using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Audiarr.Api.Data;
using Audiarr.Api.Models.DTOs;

namespace Audiarr.Api.Endpoints;

public static class ArtistEndpoints
{
    public static void MapArtistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v2/artists")
            .WithTags("Artists")
            .RequireAuthorization();

        // Get all artists with pagination
        group.MapGet("/", async (AudiarrContext db, IMemoryCache cache, int page = 1, int limit = 50) =>
        {
            if (page < 1) page = 1;
            if (limit < 1 || limit > 100) limit = 50;

            var cacheKey = $"artists:page:{page}:limit:{limit}";
            
            if (!cache.TryGetValue(cacheKey, out var cachedResult))
            {
                var query = db.Artists
                    .Include(a => a.Albums)
                    .ThenInclude(al => al.Tracks)
                    .OrderBy(a => a.SortName ?? a.Name)
                    .AsNoTracking();

                var total = await query.CountAsync();
                
                var artists = await query
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .Select(a => new ArtistDto
                    {
                        Id = a.Id,
                        Name = a.Name,
                        SortName = a.SortName,
                        AlbumCount = a.Albums.Count(),
                        TrackCount = a.Albums.SelectMany(al => al.Tracks).Count()
                    })
                    .ToListAsync();

                cachedResult = new 
                { 
                    data = artists, 
                    page, 
                    limit,
                    total,
                    totalPages = (int)Math.Ceiling((double)total / limit)
                };
                
                // Cache for 5 minutes
                cache.Set(cacheKey, cachedResult, TimeSpan.FromMinutes(5));
            }

            return Results.Ok(cachedResult);
        })
        .WithName("GetArtists")
        .WithOpenApi()
        .WithSummary("Get all artists")
        .WithDescription("Returns a paginated list of artists");

        // Get artist by ID
        group.MapGet("/{id}", async (string id, AudiarrContext db) =>
        {
            var artist = await db.Artists
                .Include(a => a.Albums)
                .ThenInclude(al => al.Tracks)
                .Where(a => a.Id == id)
                .Select(a => new 
                {
                    Id = a.Id,
                    Name = a.Name,
                    SortName = a.SortName,
                    AlbumCount = a.Albums.Count(),
                    TrackCount = a.Albums.SelectMany(al => al.Tracks).Count(),
                    Albums = a.Albums.Select(al => new AlbumDto
                    {
                        Id = al.Id,
                        Title = al.Title,
                        ArtistId = a.Id,
                        ArtistName = a.Name,
                        Year = al.Year,
                        TrackCount = al.Tracks.Count(),
                        Genre = al.Tracks.FirstOrDefault() != null ? al.Tracks.First().Genre : null,
                        CoverArtPath = al.CoverArtPath,
                        ReleaseDate = al.ReleaseDate
                    }).OrderBy(al => al.Year).ThenBy(al => al.Title)
                })
                .FirstOrDefaultAsync();

            if (artist == null)
                return Results.NotFound(new { error = "Artist not found" });

            return Results.Ok(artist);
        })
        .WithName("GetArtistById")
        .WithOpenApi()
        .WithSummary("Get artist by ID")
        .WithDescription("Returns a single artist with their albums");

        // Get artist's albums
        group.MapGet("/{id}/albums", async (string id, AudiarrContext db) =>
        {
            var artistExists = await db.Artists.AnyAsync(a => a.Id == id);
            if (!artistExists)
                return Results.NotFound(new { error = "Artist not found" });

            var albums = await db.Albums
                .Include(a => a.Artist)
                .Include(a => a.Tracks)
                .Where(a => a.ArtistId == id)
                .Select(a => new AlbumDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    ArtistId = a.ArtistId,
                    ArtistName = a.Artist.Name,
                    Year = a.Year,
                    TrackCount = a.Tracks.Count(),
                    Genre = a.Tracks.FirstOrDefault() != null ? a.Tracks.First().Genre : null,
                    CoverArtPath = a.CoverArtPath,
                    ReleaseDate = a.ReleaseDate
                })
                .OrderBy(a => a.Year)
                .ThenBy(a => a.Title)
                .ToListAsync();

            return Results.Ok(new { data = albums });
        })
        .WithName("GetArtistAlbums")
        .WithOpenApi()
        .WithSummary("Get artist's albums")
        .WithDescription("Returns all albums for a specific artist");

        // Get artist's tracks
        group.MapGet("/{id}/tracks", async (string id, AudiarrContext db) =>
        {
            var artistExists = await db.Artists.AnyAsync(a => a.Id == id);
            if (!artistExists)
                return Results.NotFound(new { error = "Artist not found" });

            var tracks = await db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Where(t => t.ArtistId == id)
                .Select(t => new TrackDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    ArtistId = t.ArtistId,
                    ArtistName = t.Artist.Name,
                    AlbumId = t.AlbumId,
                    AlbumTitle = t.Album.Title,
                    TrackNumber = t.TrackNumber,
                    DiscNumber = t.DiscNumber,
                    DurationMs = t.DurationMs,
                    Genre = t.Genre,
                    Year = t.Year,
                    FileSize = t.FileSize,
                    Bitrate = t.Bitrate,
                    Codec = t.Codec,
                    FilePath = t.FilePath
                })
                .OrderBy(t => t.AlbumTitle)
                .ThenBy(t => t.DiscNumber)
                .ThenBy(t => t.TrackNumber)
                .ToListAsync();

            return Results.Ok(new { data = tracks });
        })
        .WithName("GetArtistTracks")
        .WithOpenApi()
        .WithSummary("Get artist's tracks")
        .WithDescription("Returns all tracks for a specific artist");
    }
}