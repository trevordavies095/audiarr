using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Audiarr.Data.Context;
using Audiarr.Core.DTOs;
using Audiarr.Core.Entities;

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

                // Fetch artists with their albums and tracks
                var artistsData = await query
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .Include(a => a.Albums)
                    .ThenInclude(al => al.Tracks)
                    .ToListAsync();

                // Process in memory to avoid SQLite APPLY issues
                var artists = artistsData.Select(a => new ArtistDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    SortName = a.SortName,
                    AlbumCount = a.Albums.Count,
                    TrackCount = a.Albums.Sum(al => al.Tracks.Count)
                }).ToList();

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
            // First, fetch the artist with albums and tracks
            var artistData = await db.Artists
                .Include(a => a.Albums)
                .ThenInclude(al => al.Tracks)
                .Where(a => a.Id == id)
                .FirstOrDefaultAsync();

            if (artistData == null)
                return Results.NotFound(new { error = "Artist not found" });

            // Process the data in memory to avoid SQLite APPLY operation issues
            var albums = artistData.Albums
                .Select(al => new AlbumDto
                {
                    Id = al.Id,
                    Title = al.Title,
                    ArtistId = artistData.Id,
                    ArtistName = artistData.Name,
                    Year = al.Year,
                    TrackCount = al.Tracks.Count,
                    Genre = al.Tracks.Select(t => t.Genre).FirstOrDefault(),
                    CoverArtPath = al.CoverArtPath,
                    ReleaseDate = al.ReleaseDate
                })
                .OrderBy(al => al.Year)
                .ThenBy(al => al.Title)
                .ToList();

            var result = new
            {
                Id = artistData.Id,
                Name = artistData.Name,
                SortName = artistData.SortName,
                AlbumCount = artistData.Albums.Count,
                TrackCount = artistData.Albums.Sum(al => al.Tracks.Count),
                Albums = albums
            };

            return Results.Ok(result);
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

            // Query albums where artist is primary OR appears in many-to-many relationship
            var albumsData = await db.Albums
                .Include(a => a.Artist)
                .Include(a => a.Tracks)
                .Include(a => a.AlbumArtists)
                    .ThenInclude(aa => aa.Artist)
                .Include(a => a.AlbumGenres)
                    .ThenInclude(ag => ag.Genre)
                .Where(a => a.ArtistId == id || a.AlbumArtists.Any(aa => aa.ArtistId == id))
                .Distinct()
                .OrderBy(a => a.Year)
                .ThenBy(a => a.Title)
                .ToListAsync();

            // Map to DTOs
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
        .WithName("GetArtistAlbums")
        .WithOpenApi()
        .WithSummary("Get artist's albums")
        .WithDescription("Returns all albums for a specific artist, including albums where the artist appears as a primary or contributing artist");

        // Get artist's tracks
        group.MapGet("/{id}/tracks", async (string id, AudiarrContext db) =>
        {
            var artistExists = await db.Artists.AnyAsync(a => a.Id == id);
            if (!artistExists)
                return Results.NotFound(new { error = "Artist not found" });

            // Query tracks where artist is primary OR appears in many-to-many relationship
            var tracksData = await db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Include(t => t.TrackArtists)
                    .ThenInclude(ta => ta.Artist)
                .Include(t => t.TrackGenres)
                    .ThenInclude(tg => tg.Genre)
                .Where(t => t.ArtistId == id || t.TrackArtists.Any(ta => ta.ArtistId == id))
                .Distinct()
                .OrderBy(t => t.Album.Title)
                .ThenBy(t => t.DiscNumber)
                .ThenBy(t => t.TrackNumber)
                .ToListAsync();

            // Map to DTOs
            var tracks = tracksData.Select(t => new TrackDto
            {
                Id = t.Id,
                Title = t.Title,
                ArtistId = t.ArtistId,
                ArtistName = t.Artist.Name,
                AlbumId = t.AlbumId,
                AlbumTitle = t.Album?.Title,
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
        .WithName("GetArtistTracks")
        .WithOpenApi()
        .WithSummary("Get artist's tracks")
        .WithDescription("Returns all tracks for a specific artist, including tracks where the artist appears as a primary or contributing artist");
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
}