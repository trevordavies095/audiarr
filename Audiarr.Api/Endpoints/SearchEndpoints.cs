using Microsoft.EntityFrameworkCore;
using Audiarr.Api.Data;
using Microsoft.AspNetCore.Mvc;

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
                           a.NameNormalized.Contains(searchTerm))
                .OrderBy(a => a.Name.Length) // Prioritize shorter, more exact matches
                .ThenBy(a => a.Name)
                .Take(maxResults)
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    Type = "artist",
                    AlbumCount = db.Albums.Count(al => al.ArtistId == a.Id),
                    TrackCount = db.Tracks.Count(t => t.ArtistId == a.Id)
                })
                .ToListAsync();

            // Search albums
            var albums = await db.Albums
                .Include(a => a.Artist)
                .Where(a => a.Title.ToLower().Contains(searchTerm) || 
                           a.TitleNormalized.Contains(searchTerm))
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
                .Where(t => t.Title.ToLower().Contains(searchTerm))
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
            var query = db.Tracks.Include(t => t.Artist).Include(t => t.Album).AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                query = query.Where(t => t.Title.ToLower().Contains(request.Title.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(request.Artist))
            {
                query = query.Where(t => t.Artist.Name.ToLower().Contains(request.Artist.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(request.Album))
            {
                query = query.Where(t => t.Album != null && t.Album.Title.ToLower().Contains(request.Album.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(request.Genre))
            {
                query = query.Where(t => t.Genre != null && t.Genre.ToLower().Contains(request.Genre.ToLower()));
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
            var tracks = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    ArtistId = t.ArtistId,
                    ArtistName = t.Artist.Name,
                    AlbumId = t.AlbumId,
                    AlbumTitle = t.Album != null ? t.Album.Title : null,
                    t.Year,
                    t.Genre,
                    DurationMs = t.DurationMs,
                    t.BitRate,
                    t.TrackNumber,
                    t.DiscNumber,
                    t.FilePath
                })
                .ToListAsync();

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