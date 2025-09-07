using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Audiarr.Api.Data;
using Audiarr.Api.Models.DTOs;

namespace Audiarr.Api.Endpoints;

public static class TrackEndpoints
{
    public static void MapTrackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v2/tracks")
            .WithTags("Tracks")
            .RequireAuthorization();

        // Get all tracks with pagination
        group.MapGet("/", async (AudiarrContext db, int page = 1, int limit = 50) =>
        {
            if (page < 1) page = 1;
            if (limit < 1 || limit > 100) limit = 50;

            var query = db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .OrderBy(t => t.Artist.SortName ?? t.Artist.Name)
                .ThenBy(t => t.Album.Year)
                .ThenBy(t => t.Album.Title)
                .ThenBy(t => t.DiscNumber)
                .ThenBy(t => t.TrackNumber);

            var total = await query.CountAsync();
            
            var tracks = await query
                .Skip((page - 1) * limit)
                .Take(limit)
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
                .ToListAsync();

            return Results.Ok(new 
            { 
                data = tracks, 
                page, 
                limit,
                total,
                totalPages = (int)Math.Ceiling((double)total / limit)
            });
        })
        .WithName("GetTracks")
        .WithOpenApi()
        .WithSummary("Get all tracks")
        .WithDescription("Returns a paginated list of tracks");

        // Get track by ID
        group.MapGet("/{id}", async (string id, AudiarrContext db) =>
        {
            var track = await db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Where(t => t.Id == id)
                .Select(t => new 
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
                    SampleRate = t.SampleRate,
                    Channels = t.Channels,
                    FilePath = t.FilePath,
                    FileHash = t.FileHash,
                    AddedDate = t.AddedDate,
                    ModifiedDate = t.ModifiedDate,
                    PlayCount = t.PlayCount,
                    LastPlayedDate = t.LastPlayedDate
                })
                .FirstOrDefaultAsync();

            if (track == null)
                return Results.NotFound(new { error = "Track not found" });

            return Results.Ok(track);
        })
        .WithName("GetTrackById")
        .WithOpenApi()
        .WithSummary("Get track by ID")
        .WithDescription("Returns a single track with full metadata");

        // Stream track audio
        group.MapGet("/{id}/stream", async (string id, AudiarrContext db, HttpContext context) =>
        {
            var track = await db.Tracks
                .Where(t => t.Id == id)
                .Select(t => new { t.FilePath, t.Title, t.Codec })
                .FirstOrDefaultAsync();

            if (track == null)
                return Results.NotFound(new { error = "Track not found" });

            if (!File.Exists(track.FilePath))
                return Results.NotFound(new { error = "Audio file not found" });

            var fileInfo = new FileInfo(track.FilePath);
            var contentType = GetAudioContentType(track.FilePath);

            // Support range requests for seeking
            if (context.Request.Headers.ContainsKey("Range"))
            {
                return Results.File(
                    File.OpenRead(track.FilePath),
                    contentType: contentType,
                    fileDownloadName: null,
                    enableRangeProcessing: true
                );
            }

            return Results.File(
                await File.ReadAllBytesAsync(track.FilePath),
                contentType: contentType
            );
        })
        .WithName("StreamTrack")
        .WithOpenApi()
        .WithSummary("Stream track audio")
        .WithDescription("Streams the audio file for playback");

        // Download track
        group.MapGet("/{id}/download", async (string id, AudiarrContext db) =>
        {
            var track = await db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Where(t => t.Id == id)
                .Select(t => new 
                { 
                    t.FilePath, 
                    FileName = $"{t.Artist.Name} - {t.Album.Title} - {t.TrackNumber:00} - {t.Title}{Path.GetExtension(t.FilePath)}"
                })
                .FirstOrDefaultAsync();

            if (track == null)
                return Results.NotFound(new { error = "Track not found" });

            if (!File.Exists(track.FilePath))
                return Results.NotFound(new { error = "Audio file not found" });

            var fileBytes = await File.ReadAllBytesAsync(track.FilePath);
            var contentType = GetAudioContentType(track.FilePath);
            
            return Results.File(
                fileBytes, 
                contentType: contentType,
                fileDownloadName: SanitizeFileName(track.FileName)
            );
        })
        .WithName("DownloadTrack")
        .WithOpenApi()
        .WithSummary("Download track")
        .WithDescription("Downloads the track as a file");

        // Recently played tracks
        group.MapGet("/recent", async (AudiarrContext db, int limit = 20) =>
        {
            if (limit < 1 || limit > 100) limit = 20;

            var tracks = await db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Where(t => t.LastPlayedDate != null)
                .OrderByDescending(t => t.LastPlayedDate)
                .Take(limit)
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
                .ToListAsync();

            return Results.Ok(new { data = tracks });
        })
        .WithName("GetRecentlyPlayedTracks")
        .WithOpenApi()
        .WithSummary("Get recently played tracks")
        .WithDescription("Returns recently played tracks");

        // Most played tracks
        group.MapGet("/popular", async (AudiarrContext db, int limit = 50) =>
        {
            if (limit < 1 || limit > 100) limit = 50;

            var tracks = await db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Where(t => t.PlayCount > 0)
                .OrderByDescending(t => t.PlayCount)
                .Take(limit)
                .Select(t => new 
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
                    PlayCount = t.PlayCount,
                    LastPlayedDate = t.LastPlayedDate
                })
                .ToListAsync();

            return Results.Ok(new { data = tracks });
        })
        .WithName("GetPopularTracks")
        .WithOpenApi()
        .WithSummary("Get most played tracks")
        .WithDescription("Returns the most played tracks");

        // Update play count
        group.MapPost("/{id}/play", async (string id, AudiarrContext db) =>
        {
            var track = await db.Tracks.FindAsync(id);
            if (track == null)
                return Results.NotFound(new { error = "Track not found" });

            track.PlayCount++;
            track.LastPlayedDate = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new 
            { 
                message = "Play count updated",
                playCount = track.PlayCount,
                lastPlayedDate = track.LastPlayedDate
            });
        })
        .WithName("UpdatePlayCount")
        .WithOpenApi()
        .WithSummary("Update track play count")
        .WithDescription("Increments the play count and updates last played date");
    }

    private static string GetAudioContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".flac" => "audio/flac",
            ".ogg" => "audio/ogg",
            ".opus" => "audio/opus",
            ".wav" => "audio/wav",
            ".wma" => "audio/x-ms-wma",
            ".alac" => "audio/mp4",
            ".ape" => "audio/x-ape",
            ".wv" => "audio/wavpack",
            ".mka" => "audio/x-matroska",
            _ => "application/octet-stream"
        };
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 255 ? sanitized.Substring(0, 255) : sanitized;
    }
}