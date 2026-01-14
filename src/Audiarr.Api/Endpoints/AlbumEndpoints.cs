using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Audiarr.Data.Context;
using Audiarr.Core.DTOs;
using Audiarr.Core.Entities;

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
                .Include(a => a.AlbumArtists)
                    .ThenInclude(aa => aa.Artist)
                .Include(a => a.AlbumGenres)
                    .ThenInclude(ag => ag.Genre)
                .OrderBy(a => a.Artist.SortName ?? a.Artist.Name)
                .ThenBy(a => a.Year)
                .ThenBy(a => a.Title);

            var total = await query.CountAsync();

            // Fetch albums with tracks to avoid SQLite APPLY issues
            var albumsData = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            // Process in memory
            var albums = albumsData.Select(a => new AlbumDto
            {
                Id = a.Id,
                Title = a.Title,
                ArtistId = a.ArtistId,
                ArtistName = a.Artist.Name,
                Year = a.Year,
                TrackCount = a.Tracks.Count,
                Genre = a.Tracks.Select(t => t.Genre).FirstOrDefault(),
                CoverArtPath = a.CoverArtPath,
                ReleaseDate = a.ReleaseDate
            }).ToList();

            // Populate multi-valued tag arrays
            foreach (var album in albumsData.Zip(albums, (a, dto) => new { Album = a, Dto = dto }))
            {
                PopulateMultiValuedTags(album.Album, album.Dto);
            }

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
            // First, fetch the album with artist and tracks
            var albumData = await db.Albums
                .Include(a => a.Artist)
                .Include(a => a.Tracks)
                    .ThenInclude(t => t.Artist)
                .Include(a => a.Tracks)
                    .ThenInclude(t => t.TrackArtists)
                        .ThenInclude(ta => ta.Artist)
                .Include(a => a.Tracks)
                    .ThenInclude(t => t.TrackGenres)
                        .ThenInclude(tg => tg.Genre)
                .Include(a => a.AlbumArtists)
                    .ThenInclude(aa => aa.Artist)
                .Include(a => a.AlbumGenres)
                    .ThenInclude(ag => ag.Genre)
                .Where(a => a.Id == id)
                .FirstOrDefaultAsync();

            if (albumData == null)
                return Results.NotFound(new { error = "Album not found" });

            // Process the data in memory to avoid SQLite APPLY operation issues
            var tracksData = albumData.Tracks
                .OrderBy(t => t.DiscNumber)
                .ThenBy(t => t.TrackNumber)
                .ToList();

            var tracks = tracksData.Select(t => new TrackDto
            {
                Id = t.Id,
                Title = t.Title,
                ArtistId = t.ArtistId,
                ArtistName = t.Artist?.Name ?? string.Empty,
                AlbumId = t.AlbumId,
                AlbumTitle = albumData.Title,
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

            // Populate multi-valued tag arrays for tracks
            foreach (var track in tracksData.Zip(tracks, (t, dto) => new { Track = t, Dto = dto }))
            {
                PopulateMultiValuedTags(track.Track, track.Dto);
            }

            // Create AlbumDto and populate multi-valued tags
            var albumDto = new AlbumDto
            {
                Id = albumData.Id,
                Title = albumData.Title,
                ArtistId = albumData.ArtistId,
                ArtistName = albumData.Artist.Name,
                Year = albumData.Year,
                TrackCount = albumData.Tracks.Count,
                Genre = albumData.Tracks.Select(t => t.Genre).FirstOrDefault(),
                CoverArtPath = albumData.CoverArtPath,
                ReleaseDate = albumData.ReleaseDate
            };

            PopulateMultiValuedTags(albumData, albumDto);

            var result = new
            {
                albumDto.Id,
                albumDto.Title,
                albumDto.ArtistId,
                albumDto.ArtistName,
                albumDto.Year,
                albumDto.TrackCount,
                albumDto.Genre,
                albumDto.CoverArtPath,
                albumDto.ReleaseDate,
                TotalDurationMs = albumData.Tracks.Sum(t => t.DurationMs),
                Tracks = tracks,
                // Multi-valued tag arrays
                albumDto.ArtistIds,
                albumDto.ArtistNames,
                albumDto.Genres,
                albumDto.PrimaryArtistId,
                albumDto.PrimaryArtistName
            };

            return Results.Ok(result);
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

            // Load full entities with navigation properties to populate multi-valued tags
            var tracksData = await db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Include(t => t.TrackArtists)
                    .ThenInclude(ta => ta.Artist)
                .Include(t => t.TrackGenres)
                    .ThenInclude(tg => tg.Genre)
                .Where(t => t.AlbumId == id)
                .OrderBy(t => t.DiscNumber)
                .ThenBy(t => t.TrackNumber)
                .ToListAsync();

            // Map to DTOs
            var tracks = tracksData.Select(t => new TrackDto
            {
                Id = t.Id,
                Title = t.Title,
                ArtistId = t.ArtistId,
                ArtistName = t.Artist?.Name ?? string.Empty,
                AlbumId = t.AlbumId,
                AlbumTitle = t.Album?.Title ?? string.Empty,
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

            // Fetch albums with related data
            var albumsData = await db.Albums
                .Include(a => a.Artist)
                .Include(a => a.Tracks)
                .Include(a => a.AlbumArtists)
                    .ThenInclude(aa => aa.Artist)
                .Include(a => a.AlbumGenres)
                    .ThenInclude(ag => ag.Genre)
                .OrderByDescending(a => a.AddedDate)
                .Take(limit)
                .ToListAsync();

            // Process in memory to avoid SQLite APPLY issues
            var albums = albumsData.Select(a => new AlbumDto
            {
                Id = a.Id,
                Title = a.Title,
                ArtistId = a.ArtistId,
                ArtistName = a.Artist.Name,
                Year = a.Year,
                TrackCount = a.Tracks.Count,
                Genre = a.Tracks.Select(t => t.Genre).FirstOrDefault(),
                CoverArtPath = a.CoverArtPath,
                ReleaseDate = a.ReleaseDate
            }).ToList();

            // Populate multi-valued tag arrays
            foreach (var album in albumsData.Zip(albums, (a, dto) => new { Album = a, Dto = dto }))
            {
                PopulateMultiValuedTags(album.Album, album.Dto);
            }

            return Results.Ok(new { data = albums });
        })
        .WithName("GetRecentAlbums")
        .WithOpenApi()
        .WithSummary("Get recently added albums")
        .WithDescription("Returns recently added albums");
    }

    /// <summary>
    /// Populates multi-valued tag arrays (ArtistIds, ArtistNames, Genres) in an AlbumDto from an Album entity.
    /// Ensures primary artist/genre (matching Album.ArtistId and dto.Genre) appears first in arrays.
    /// </summary>
    private static void PopulateMultiValuedTags(Album album, AlbumDto dto)
    {
        // Populate artist arrays: Primary first (matching Album.ArtistId), then others alphabetically
        var primaryArtist = album.AlbumArtists
            .FirstOrDefault(aa => aa.ArtistId == album.ArtistId)?.Artist;
        var otherArtists = album.AlbumArtists
            .Where(aa => aa.ArtistId != album.ArtistId)
            .Select(aa => aa.Artist)
            .OrderBy(a => a.Name)
            .ToList();

        var allArtists = primaryArtist != null
            ? new[] { primaryArtist }.Concat(otherArtists).ToList()
            : otherArtists;

        dto.ArtistIds = allArtists.Select(a => a.Id).ToArray();
        dto.ArtistNames = allArtists.Select(a => a.Name).ToArray();

        // Ensure primary artist from single-value field is included first, even if not in many-to-many relationship
        if (!string.IsNullOrEmpty(album.ArtistId))
        {
            if (dto.ArtistIds.Length == 0)
            {
                // No artists in many-to-many relationship, use single-value field
                dto.ArtistIds = new[] { album.ArtistId };
                dto.ArtistNames = new[] { album.Artist?.Name ?? string.Empty };
            }
            else if (!dto.ArtistIds.Contains(album.ArtistId))
            {
                // Primary artist exists but is not in many-to-many relationship, prepend it
                dto.ArtistIds = new[] { album.ArtistId }.Concat(dto.ArtistIds).ToArray();
                dto.ArtistNames = new[] { album.Artist?.Name ?? string.Empty }.Concat(dto.ArtistNames).ToArray();
            }
        }

        // Populate genre arrays: Primary first (matching dto.Genre name), then others alphabetically
        // Use dto.Genre (which is set from first track's genre) instead of album.Genre to ensure consistency
        var primaryGenre = album.AlbumGenres
            .FirstOrDefault(ag => ag.Genre.Name == dto.Genre)?.Genre;
        var otherGenres = album.AlbumGenres
            .Where(ag => ag.Genre.Name != dto.Genre)
            .Select(ag => ag.Genre)
            .OrderBy(g => g.Name)
            .ToList();

        var allGenres = primaryGenre != null
            ? new[] { primaryGenre }.Concat(otherGenres).ToList()
            : otherGenres;

        dto.Genres = allGenres.Select(g => g.Name).ToArray();

        // Ensure primary genre from single-value field is included first, even if not in many-to-many relationship
        if (!string.IsNullOrEmpty(dto.Genre))
        {
            if (dto.Genres.Length == 0)
            {
                // No genres in many-to-many relationship, use single-value field
                dto.Genres = new[] { dto.Genre };
            }
            else if (!dto.Genres.Contains(dto.Genre))
            {
                // Primary genre exists but is not in many-to-many relationship, prepend it
                dto.Genres = new[] { dto.Genre }.Concat(dto.Genres).ToArray();
            }
        }
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

        // Ensure primary artist from single-value field is included first, even if not in many-to-many relationship
        if (!string.IsNullOrEmpty(track.ArtistId))
        {
            if (dto.ArtistIds.Length == 0)
            {
                // No artists in many-to-many relationship, use single-value field
                dto.ArtistIds = new[] { track.ArtistId };
                dto.ArtistNames = new[] { track.Artist?.Name ?? string.Empty };
            }
            else if (!dto.ArtistIds.Contains(track.ArtistId))
            {
                // Primary artist exists but is not in many-to-many relationship, prepend it
                dto.ArtistIds = new[] { track.ArtistId }.Concat(dto.ArtistIds).ToArray();
                dto.ArtistNames = new[] { track.Artist?.Name ?? string.Empty }.Concat(dto.ArtistNames).ToArray();
            }
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

        // Ensure primary genre from single-value field is included first, even if not in many-to-many relationship
        if (!string.IsNullOrEmpty(track.Genre))
        {
            if (dto.Genres.Length == 0)
            {
                // No genres in many-to-many relationship, use single-value field
                dto.Genres = new[] { track.Genre };
            }
            else if (!dto.Genres.Contains(track.Genre))
            {
                // Primary genre exists but is not in many-to-many relationship, prepend it
                dto.Genres = new[] { track.Genre }.Concat(dto.Genres).ToArray();
            }
        }
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