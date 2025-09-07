using Microsoft.EntityFrameworkCore;
using Audiarr.Api.Data;

namespace Audiarr.Api.Endpoints;

public static class DiagnosticEndpoints
{
    public static void MapDiagnosticEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v2/diagnostic");

        group.MapGet("/data-check", async (AudiarrContext db) =>
        {
            var artistCount = await db.Artists.CountAsync();
            var albumCount = await db.Albums.CountAsync();
            var trackCount = await db.Tracks.CountAsync();
            
            // Check for duplicates
            var artistsRaw = await db.Artists.ToListAsync();
            var artistGroups = artistsRaw.GroupBy(a => a.Name).Where(g => g.Count() > 1).ToList();
            
            var albumsRaw = await db.Albums.Include(a => a.Artist).ToListAsync();
            var albumGroups = albumsRaw.GroupBy(a => new { a.Title, a.ArtistId }).Where(g => g.Count() > 1).ToList();
            
            // Sample data
            var sampleArtists = await db.Artists.Take(5).Select(a => new 
            {
                a.Id,
                a.Name,
                AlbumCount = db.Albums.Count(al => al.ArtistId == a.Id),
                TrackCount = db.Tracks.Count(t => t.ArtistId == a.Id)
            }).ToListAsync();
            
            return new
            {
                TotalCounts = new { artistCount, albumCount, trackCount },
                DuplicateArtists = artistGroups.Select(g => new 
                { 
                    Name = g.Key, 
                    Count = g.Count(),
                    Ids = g.Select(a => a.Id).ToList()
                }),
                DuplicateAlbums = albumGroups.Select(g => new 
                { 
                    Title = g.Key.Title,
                    ArtistId = g.Key.ArtistId,
                    Count = g.Count(),
                    Ids = g.Select(a => a.Id).ToList()
                }),
                SampleArtists = sampleArtists
            };
        });
    }
}