using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Audiarr.Data.Context;
using Audiarr.Core.DTOs;

namespace Audiarr.Api.Endpoints;

public static class AlbumEndpoints
{
    public static void MapAlbumEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v2/albums")
            .WithTags("Albums")
            .RequireAuthorization();

        // Get all albums with pagination
        group.MapGet("/", async (AudiarrContext db, int page = 1, int limit = 50) =>
        {
            if (page < 1) page = 1;
            if (limit < 1 || limit > 100) limit = 50;

            var query = db.Albums
                .Include(a => a.Artist)
                .Include(a => a.Tracks)
                .OrderBy(a => a.Artist.SortName ?? a.Artist.Name)
                .ThenBy(a => a.Year)
                .ThenBy(a => a.Title);

            var total = await query.CountAsync();

            var albums = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(a => new AlbumDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    ArtistId = a.ArtistId,
                    ArtistName = a.Artist.Name,
                    Year = a.Year,
                    TrackCount = a.Tracks.Count(),
                    Genre = a.Tracks.Select(t => t.Genre).FirstOrDefault(),
                    CoverArtPath = a.CoverArtPath,
                    ReleaseDate = a.ReleaseDate
                })
                .ToListAsync();

            return Results.Ok(new
            {
                data = albums,
                page,
                limit,
                total,
                totalPages = (int)Math.Ceiling((double)total / limit)
            });
        })
        .WithName("GetAlbums")
        .WithOpenApi()
        .WithSummary("Get all albums")
        .WithDescription("Returns a paginated list of albums");

        // Get album by ID
        group.MapGet("/{id}", async (string id, AudiarrContext db) =>
        {
            var album = await db.Albums
                .Include(a => a.Artist)
                .Include(a => a.Tracks)
                .Where(a => a.Id == id)
                .Select(a => new
                {
                    Id = a.Id,
                    Title = a.Title,
                    ArtistId = a.ArtistId,
                    ArtistName = a.Artist.Name,
                    Year = a.Year,
                    TrackCount = a.Tracks.Count(),
                    Genre = a.Tracks.Select(t => t.Genre).FirstOrDefault(),
                    CoverArtPath = a.CoverArtPath,
                    ReleaseDate = a.ReleaseDate,
                    TotalDurationMs = a.Tracks.Sum(t => t.DurationMs),
                    Tracks = a.Tracks.Select(t => new TrackDto
                    {
                        Id = t.Id,
                        Title = t.Title,
                        ArtistId = t.ArtistId,
                        ArtistName = a.Artist.Name,
                        AlbumId = t.AlbumId,
                        AlbumTitle = a.Title,
                        TrackNumber = t.TrackNumber,
                        DiscNumber = t.DiscNumber,
                        DurationMs = t.DurationMs,
                        Genre = t.Genre,
                        Year = t.Year,
                        FileSize = t.FileSize,
                        Bitrate = t.Bitrate,
                        Codec = t.Codec,
                        FilePath = t.FilePath
                    }).OrderBy(t => t.DiscNumber).ThenBy(t => t.TrackNumber).ToList()
                })
                .FirstOrDefaultAsync();

            if (album == null)
                return Results.NotFound(new { error = "Album not found" });

            return Results.Ok(album);
        })
        .WithName("GetAlbumById")
        .WithOpenApi()
        .WithSummary("Get album by ID")
        .WithDescription("Returns a single album with its tracks");

        // Get album tracks
        group.MapGet("/{id}/tracks", async (string id, AudiarrContext db) =>
        {
            var albumExists = await db.Albums.AnyAsync(a => a.Id == id);
            if (!albumExists)
                return Results.NotFound(new { error = "Album not found" });

            var tracks = await db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Where(t => t.AlbumId == id)
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
                .OrderBy(t => t.DiscNumber)
                .ThenBy(t => t.TrackNumber)
                .ToListAsync();

            return Results.Ok(new { data = tracks });
        })
        .WithName("GetAlbumTracks")
        .WithOpenApi()
        .WithSummary("Get album tracks")
        .WithDescription("Returns all tracks for a specific album");

        // Get album cover art
        group.MapGet("/{id}/cover", async (string id, AudiarrContext db, IWebHostEnvironment env) =>
        {
            var album = await db.Albums
                .Where(a => a.Id == id)
                .Select(a => new { a.CoverArtPath })
                .FirstOrDefaultAsync();

            if (album == null)
                return Results.NotFound(new { error = "Album not found" });

            if (string.IsNullOrEmpty(album.CoverArtPath))
            {
                // Return a default cover image if none exists
                var defaultCoverPath = Path.Combine(env.WebRootPath, "images", "default-album.png");
                if (File.Exists(defaultCoverPath))
                {
                    var defaultBytes = await File.ReadAllBytesAsync(defaultCoverPath);
                    return Results.File(defaultBytes, "image/png");
                }
                return Results.NotFound(new { error = "No cover art available" });
            }

            // Convert the stored path (/artwork/filename.jpg) to actual file path
            string actualFilePath;
            if (album.CoverArtPath.StartsWith("/artwork/"))
            {
                var filename = album.CoverArtPath.Substring("/artwork/".Length);
                // Use same logic as LibraryScanner for consistency
                var baseDir = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
                    ? "/data"
                    : Path.Combine(Directory.GetCurrentDirectory(), "Data");
                actualFilePath = Path.Combine(baseDir, "artwork", filename);
            }
            else
            {
                actualFilePath = album.CoverArtPath;
            }

            // Check if cover art file exists
            if (!File.Exists(actualFilePath))
            {
                return Results.NotFound(new { error = $"Cover art file not found: {album.CoverArtPath}" });
            }

            var fileBytes = await File.ReadAllBytesAsync(actualFilePath);
            var contentType = GetImageContentType(actualFilePath);

            return Results.File(fileBytes, contentType);
        })
        .WithName("GetAlbumCover")
        .WithOpenApi()
        .WithSummary("Get album cover art")
        .WithDescription("Returns the album cover art image")
        .AllowAnonymous(); // Allow anonymous access to cover art

        // Recent albums
        group.MapGet("/recent", async (AudiarrContext db, int limit = 20) =>
        {
            if (limit < 1 || limit > 100) limit = 20;

            var albums = await db.Albums
                .Include(a => a.Artist)
                .Include(a => a.Tracks)
                .OrderByDescending(a => a.AddedDate)
                .Take(limit)
                .Select(a => new AlbumDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    ArtistId = a.ArtistId,
                    ArtistName = a.Artist.Name,
                    Year = a.Year,
                    TrackCount = a.Tracks.Count(),
                    Genre = a.Tracks.Select(t => t.Genre).FirstOrDefault(),
                    CoverArtPath = a.CoverArtPath,
                    ReleaseDate = a.ReleaseDate
                })
                .ToListAsync();

            return Results.Ok(new { data = albums });
        })
        .WithName("GetRecentAlbums")
        .WithOpenApi()
        .WithSummary("Get recently added albums")
        .WithDescription("Returns recently added albums");
    }

    private static string GetImageContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
    }
}