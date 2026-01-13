using Microsoft.EntityFrameworkCore;
using Audiarr.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Audiarr.Core.DTOs;
using Audiarr.Core.Entities;

namespace Audiarr.Api.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v2/search");

        group.MapGet("/", async (
            [FromQuery] string q,
            [FromQuery] int? limit,
            AudiarrContext db) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { error = "Search query cannot be empty" });
            }

            var maxResults = limit ?? 5;
            var searchTerm = q.ToLower();

            // Search artists
            var artists = await db.Artists
                .Where(a => a.Name.ToLower().Contains(searchTerm) ||
                           (a.NameNormalized != null && a.NameNormalized.Contains(searchTerm)))
                .OrderBy(a => a.Name.Length) // Prioritize shorter, more exact matches
                .ThenBy(a => a.Name)
                .Take(maxResults)
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    Type = "artist",
                    AlbumCount = db.Albums.Count(al => al.ArtistId == a.Id || al.AlbumArtists.Any(aa => aa.ArtistId == a.Id)),
                    TrackCount = db.Tracks.Count(t => t.ArtistId == a.Id || t.TrackArtists.Any(ta => ta.ArtistId == a.Id))
                })
                .ToListAsync();

            // Search albums
            var albums = await db.Albums
                .Include(a => a.Artist)
                .Include(a => a.AlbumArtists)
                    .ThenInclude(aa => aa.Artist)
                .Include(a => a.AlbumGenres)
                    .ThenInclude(ag => ag.Genre)
                .Where(a => a.Title.ToLower().Contains(searchTerm) ||
                           (a.TitleNormalized != null && a.TitleNormalized.Contains(searchTerm)) ||
                           a.Artist.Name.ToLower().Contains(searchTerm) ||
                           (a.Genre != null && a.Genre.ToLower().Contains(searchTerm)) ||
                           a.AlbumArtists.Any(aa => aa.Artist.Name.ToLower().Contains(searchTerm)) ||
                           a.AlbumGenres.Any(ag => ag.Genre.Name.ToLower().Contains(searchTerm)))
                .OrderBy(a => a.Title.Length)
                .ThenBy(a => a.Title)
                .Take(maxResults)
                .Select(a => new
                {
                    a.Id,
                    Title = a.Title,
                    Type = "album",
                    ArtistId = a.ArtistId,
                    ArtistName = a.Artist.Name,
                    Year = a.Year ?? a.ReleaseYear,
                    a.CoverArtPath,
                    TrackCount = db.Tracks.Count(t => t.AlbumId == a.Id)
                })
                .ToListAsync();

            // Search tracks
            var tracks = await db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Include(t => t.TrackArtists)
                    .ThenInclude(ta => ta.Artist)
                .Include(t => t.TrackGenres)
                    .ThenInclude(tg => tg.Genre)
                .Where(t => t.Title.ToLower().Contains(searchTerm) ||
                           t.Artist.Name.ToLower().Contains(searchTerm) ||
                           (t.Genre != null && t.Genre.ToLower().Contains(searchTerm)) ||
                           t.TrackArtists.Any(ta => ta.Artist.Name.ToLower().Contains(searchTerm)) ||
                           t.TrackGenres.Any(tg => tg.Genre.Name.ToLower().Contains(searchTerm)))
                .OrderBy(t => t.Title.Length)
                .ThenBy(t => t.Title)
                .Take(maxResults)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    Type = "track",
                    ArtistId = t.ArtistId,
                    ArtistName = t.Artist.Name,
                    AlbumId = t.AlbumId,
                    AlbumTitle = t.Album != null ? t.Album.Title : null,
                    DurationMs = t.DurationMs,
                    t.TrackNumber,
                    t.DiscNumber
                })
                .ToListAsync();

            var totalResults = artists.Count + albums.Count + tracks.Count;

            return Results.Ok(new
            {
                query = q,
                totalResults,
                artists,
                albums,
                tracks
            });
        })
        .WithName("Search")
        .WithOpenApi()
        .WithSummary("Search for artists, albums, and tracks")
        .WithDescription("Searches across all music entities and returns matching results");

        // Advanced search endpoint with filters
        group.MapPost("/advanced", async (
            [FromBody] AdvancedSearchRequest request,
            AudiarrContext db) =>
        {
            var query = db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Include(t => t.TrackArtists)
                    .ThenInclude(ta => ta.Artist)
                .Include(t => t.TrackGenres)
                    .ThenInclude(tg => tg.Genre)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                query = query.Where(t => t.Title.ToLower().Contains(request.Title.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(request.Artist))
            {
                var artistLower = request.Artist.ToLower();
                query = query.Where(t => 
                    t.Artist.Name.ToLower().Contains(artistLower) ||
                    t.TrackArtists.Any(ta => ta.Artist.Name.ToLower().Contains(artistLower))
                );
            }

            if (!string.IsNullOrWhiteSpace(request.Album))
            {
                query = query.Where(t => t.Album != null && t.Album.Title.ToLower().Contains(request.Album.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(request.Genre))
            {
                var genreLower = request.Genre.ToLower();
                query = query.Where(t => 
                    (t.Genre != null && t.Genre.ToLower().Contains(genreLower)) ||
                    t.TrackGenres.Any(tg => tg.Genre.Name.ToLower().Contains(genreLower))
                );
            }

            if (request.YearFrom.HasValue)
            {
                query = query.Where(t => t.Year >= request.YearFrom.Value);
            }

            if (request.YearTo.HasValue)
            {
                query = query.Where(t => t.Year <= request.YearTo.Value);
            }

            if (request.MinBitrate.HasValue)
            {
                query = query.Where(t => t.BitRate >= request.MinBitrate.Value);
            }

            // Sorting
            query = request.SortBy?.ToLower() switch
            {
                "title" => request.SortDescending ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
                "artist" => request.SortDescending ? query.OrderByDescending(t => t.Artist.Name) : query.OrderBy(t => t.Artist.Name),
                "album" => request.SortDescending ? query.OrderByDescending(t => t.Album!.Title) : query.OrderBy(t => t.Album!.Title),
                "year" => request.SortDescending ? query.OrderByDescending(t => t.Year) : query.OrderBy(t => t.Year),
                "duration" => request.SortDescending ? query.OrderByDescending(t => t.DurationMs) : query.OrderBy(t => t.DurationMs),
                _ => query.OrderBy(t => t.Title)
            };

            var totalCount = await query.CountAsync();

            // Pagination
            var pageSize = request.PageSize ?? 50;
            var page = request.Page ?? 1;
            
            // Load full entities to populate multi-valued tags
            var tracksData = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Map to DTOs and populate multi-valued tag arrays
            var tracks = tracksData.Select(t => new TrackDto
            {
                Id = t.Id,
                Title = t.Title,
                ArtistId = t.ArtistId,
                ArtistName = t.Artist.Name,
                AlbumId = t.AlbumId,
                AlbumTitle = t.Album != null ? t.Album.Title : null,
                Year = t.Year,
                Genre = t.Genre,
                DurationMs = t.DurationMs,
                Bitrate = t.Bitrate,
                TrackNumber = t.TrackNumber,
                DiscNumber = t.DiscNumber,
                FilePath = t.FilePath,
                FileSize = t.FileSize,
                Codec = t.Codec
            }).ToList();

            // Populate multi-valued tag arrays
            foreach (var track in tracksData.Zip(tracks, (t, dto) => new { Track = t, Dto = dto }))
            {
                PopulateMultiValuedTags(track.Track, track.Dto);
            }

            return Results.Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                tracks
            });
        })
        .WithName("AdvancedSearch")
        .WithOpenApi()
        .WithSummary("Advanced search with filters")
        .WithDescription("Perform advanced search with multiple filter options");

        // Quick search suggestions endpoint
        group.MapGet("/suggestions", async (
            [FromQuery] string q,
            AudiarrContext db) =>
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            {
                return Results.Ok(new { suggestions = Array.Empty<object>() });
            }

            var searchTerm = q.ToLower();
            var suggestions = new List<object>();

            // Get top artist matches
            var artistSuggestions = await db.Artists
                .Where(a => a.Name.ToLower().StartsWith(searchTerm))
                .OrderBy(a => a.Name)
                .Take(3)
                .Select(a => new
                {
                    Value = a.Name,
                    Type = "artist",
                    Id = a.Id
                })
                .ToListAsync();
            suggestions.AddRange(artistSuggestions);

            // Get top album matches
            var albumSuggestions = await db.Albums
                .Where(a => a.Title.ToLower().StartsWith(searchTerm))
                .OrderBy(a => a.Title)
                .Take(3)
                .Select(a => new
                {
                    Value = a.Title,
                    Type = "album",
                    Id = a.Id
                })
                .ToListAsync();
            suggestions.AddRange(albumSuggestions);

            // Get top track matches
            var trackSuggestions = await db.Tracks
                .Where(t => t.Title.ToLower().StartsWith(searchTerm))
                .OrderBy(t => t.Title)
                .Take(3)
                .Select(t => new
                {
                    Value = t.Title,
                    Type = "track",
                    Id = t.Id
                })
                .ToListAsync();
            suggestions.AddRange(trackSuggestions);

            return Results.Ok(new { suggestions });
        })
        .WithName("SearchSuggestions")
        .WithOpenApi()
        .WithSummary("Get search suggestions")
        .WithDescription("Returns autocomplete suggestions based on partial search query");
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

    public class AdvancedSearchRequest
    {
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? Genre { get; set; }
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public int? MinBitrate { get; set; }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }
}