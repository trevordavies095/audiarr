using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Audiarr.Data.Context;
using Audiarr.Core.DTOs;
using Audiarr.Core.Entities;

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
                .Include(t => t.TrackArtists)
                    .ThenInclude(ta => ta.Artist)
                .Include(t => t.TrackGenres)
                    .ThenInclude(tg => tg.Genre)
                .OrderBy(t => t.Artist.SortName ?? t.Artist.Name)
                .ThenBy(t => t.Album.Year)
                .ThenBy(t => t.Album.Title)
                .ThenBy(t => t.DiscNumber)
                .ThenBy(t => t.TrackNumber);

            var total = await query.CountAsync();

            // Load full entities to populate multi-valued tags
            var tracksData = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            // Map to DTOs and populate arrays
            var tracks = tracksData.Select(t => new TrackDto
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
            }).ToList();

            // Populate multi-valued tag arrays
            foreach (var track in tracksData.Zip(tracks, (t, dto) => new { Track = t, Dto = dto }))
            {
                PopulateMultiValuedTags(track.Track, track.Dto);
            }

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
                .Include(t => t.TrackArtists)
                    .ThenInclude(ta => ta.Artist)
                .Include(t => t.TrackGenres)
                    .ThenInclude(tg => tg.Genre)
                .Where(t => t.Id == id)
                .FirstOrDefaultAsync();

            if (track == null)
                return Results.NotFound(new { error = "Track not found" });

            var dto = new TrackDto
            {
                Id = track.Id,
                Title = track.Title,
                ArtistId = track.ArtistId,
                ArtistName = track.Artist.Name,
                AlbumId = track.AlbumId,
                AlbumTitle = track.Album.Title,
                TrackNumber = track.TrackNumber,
                DiscNumber = track.DiscNumber,
                DurationMs = track.DurationMs,
                Genre = track.Genre,
                Year = track.Year,
                FileSize = track.FileSize,
                Bitrate = track.Bitrate,
                Codec = track.Codec,
                FilePath = track.FilePath
            };

            PopulateMultiValuedTags(track, dto);

            // Return extended response with additional fields for backward compatibility
            var result = new
            {
                dto.Id,
                dto.Title,
                dto.ArtistId,
                dto.ArtistName,
                dto.AlbumId,
                dto.AlbumTitle,
                dto.TrackNumber,
                dto.DiscNumber,
                dto.DurationMs,
                dto.Genre,
                dto.Year,
                dto.FileSize,
                dto.Bitrate,
                dto.Codec,
                SampleRate = track.SampleRate,
                Channels = track.Channels,
                FilePath = dto.FilePath,
                FileHash = track.FileHash,
                AddedDate = track.AddedDate,
                ModifiedDate = track.ModifiedDate,
                PlayCount = track.PlayCount,
                LastPlayedDate = track.LastPlayedDate,
                // Multi-valued tag arrays
                dto.ArtistIds,
                dto.ArtistNames,
                dto.Genres,
                dto.PrimaryArtistId,
                dto.PrimaryArtistName
            };

            return Results.Ok(result);
        })
        .WithName("GetTrackById")
        .WithOpenApi()
        .WithSummary("Get track by ID")
        .WithDescription("Returns a single track with full metadata");

        // Note: Streaming endpoint has been moved to StreamEndpoints.cs with enhanced range request support

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

            var tracksData = await db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Include(t => t.TrackArtists)
                    .ThenInclude(ta => ta.Artist)
                .Include(t => t.TrackGenres)
                    .ThenInclude(tg => tg.Genre)
                .Where(t => t.LastPlayedDate != null)
                .OrderByDescending(t => t.LastPlayedDate)
                .Take(limit)
                .ToListAsync();

            var tracks = tracksData.Select(t => new TrackDto
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
            }).ToList();

            // Populate multi-valued tag arrays
            foreach (var track in tracksData.Zip(tracks, (t, dto) => new { Track = t, Dto = dto }))
            {
                PopulateMultiValuedTags(track.Track, track.Dto);
            }

            // Create extended response with PlayCount and LastPlayedDate for backward compatibility
            var result = tracksData.Zip(tracks, (t, dto) => new
            {
                dto.Id,
                dto.Title,
                dto.ArtistId,
                dto.ArtistName,
                dto.AlbumId,
                dto.AlbumTitle,
                dto.TrackNumber,
                dto.DiscNumber,
                dto.DurationMs,
                dto.Genre,
                dto.Year,
                dto.FileSize,
                dto.Bitrate,
                dto.Codec,
                dto.FilePath,
                PlayCount = t.PlayCount,
                LastPlayedDate = t.LastPlayedDate,
                // Multi-valued tag arrays
                dto.ArtistIds,
                dto.ArtistNames,
                dto.Genres,
                dto.PrimaryArtistId,
                dto.PrimaryArtistName
            }).ToList();

            return Results.Ok(new { data = result });
        })
        .WithName("GetRecentlyPlayedTracks")
        .WithOpenApi()
        .WithSummary("Get recently played tracks")
        .WithDescription("Returns recently played tracks");

        // Most played tracks
        group.MapGet("/popular", async (AudiarrContext db, int limit = 50) =>
        {
            if (limit < 1 || limit > 100) limit = 50;

            var tracksData = await db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Include(t => t.TrackArtists)
                    .ThenInclude(ta => ta.Artist)
                .Include(t => t.TrackGenres)
                    .ThenInclude(tg => tg.Genre)
                .Where(t => t.PlayCount > 0)
                .OrderByDescending(t => t.PlayCount)
                .Take(limit)
                .ToListAsync();

            var tracks = tracksData.Select(t => new TrackDto
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
            }).ToList();

            // Populate multi-valued tag arrays
            foreach (var track in tracksData.Zip(tracks, (t, dto) => new { Track = t, Dto = dto }))
            {
                PopulateMultiValuedTags(track.Track, track.Dto);
            }

            // Create extended response with PlayCount and LastPlayedDate for backward compatibility
            var result = tracksData.Zip(tracks, (t, dto) => new
            {
                dto.Id,
                dto.Title,
                dto.ArtistId,
                dto.ArtistName,
                dto.AlbumId,
                dto.AlbumTitle,
                dto.TrackNumber,
                dto.DiscNumber,
                dto.DurationMs,
                dto.Genre,
                dto.Year,
                dto.FileSize,
                dto.Bitrate,
                dto.Codec,
                dto.FilePath,
                PlayCount = t.PlayCount,
                LastPlayedDate = t.LastPlayedDate,
                // Multi-valued tag arrays
                dto.ArtistIds,
                dto.ArtistNames,
                dto.Genres,
                dto.PrimaryArtistId,
                dto.PrimaryArtistName
            }).ToList();

            return Results.Ok(new { data = result });
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

    /// <summary>
    /// Populates multi-valued tag arrays (ArtistIds, ArtistNames, Genres) in a TrackDto from a Track entity.
    /// Ensures primary artist/genre (matching Track.ArtistId and Track.Genre) appears first in arrays.
    /// </summary>
    private static void PopulateMultiValuedTags(Track track, TrackDto dto)
    {
        // Populate artist arrays: Primary first (matching Track.ArtistId), then others alphabetically
        var primaryArtist = track.TrackArtists
            .FirstOrDefault(ta => ta.ArtistId == track.ArtistId)?.Artist;
        var otherArtists = track.TrackArtists
            .Where(ta => ta.ArtistId != track.ArtistId)
            .Select(ta => ta.Artist)
            .OrderBy(a => a.Name)
            .ToList();

        var allArtists = primaryArtist != null
            ? new[] { primaryArtist }.Concat(otherArtists).ToList()
            : otherArtists;

        dto.ArtistIds = allArtists.Select(a => a.Id).ToArray();
        dto.ArtistNames = allArtists.Select(a => a.Name).ToArray();

        // If no artists in many-to-many relationship, fallback to single-value field
        if (dto.ArtistIds.Length == 0 && !string.IsNullOrEmpty(track.ArtistId))
        {
            dto.ArtistIds = new[] { track.ArtistId };
            dto.ArtistNames = new[] { track.Artist?.Name ?? string.Empty };
        }

        // Populate genre arrays: Primary first (matching Track.Genre name), then others alphabetically
        var primaryGenre = track.TrackGenres
            .FirstOrDefault(tg => tg.Genre.Name == track.Genre)?.Genre;
        var otherGenres = track.TrackGenres
            .Where(tg => tg.Genre.Name != track.Genre)
            .Select(tg => tg.Genre)
            .OrderBy(g => g.Name)
            .ToList();

        var allGenres = primaryGenre != null
            ? new[] { primaryGenre }.Concat(otherGenres).ToList()
            : otherGenres;

        dto.Genres = allGenres.Select(g => g.Name).ToArray();

        // If no genres in many-to-many relationship, fallback to single-value field
        if (dto.Genres.Length == 0 && !string.IsNullOrEmpty(track.Genre))
        {
            dto.Genres = new[] { track.Genre };
        }
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