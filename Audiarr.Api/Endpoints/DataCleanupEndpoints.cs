using Microsoft.EntityFrameworkCore;
using Audiarr.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace Audiarr.Api.Endpoints;

public static class DataCleanupEndpoints
{
    public static void MapDataCleanupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v2/cleanup");

        group.MapPost("/merge-duplicate-artists", async (AudiarrContext db) =>
        {
            var mergeCount = 0;
            
            // Find all duplicate artists by name
            var duplicateGroups = await db.Artists
                .GroupBy(a => a.Name)
                .Where(g => g.Count() > 1)
                .Select(g => new { Name = g.Key, Artists = g.ToList() })
                .ToListAsync();
            
            foreach (var group in duplicateGroups)
            {
                // Keep the first artist, merge all others into it
                var primaryArtist = group.Artists.First();
                var duplicateArtists = group.Artists.Skip(1).ToList();
                
                foreach (var duplicate in duplicateArtists)
                {
                    // Update all tracks to point to primary artist
                    await db.Tracks
                        .Where(t => t.ArtistId == duplicate.Id)
                        .ExecuteUpdateAsync(t => t.SetProperty(x => x.ArtistId, primaryArtist.Id));
                    
                    // Update all albums to point to primary artist
                    await db.Albums
                        .Where(a => a.ArtistId == duplicate.Id)
                        .ExecuteUpdateAsync(a => a.SetProperty(x => x.ArtistId, primaryArtist.Id));
                    
                    // Remove duplicate artist
                    db.Artists.Remove(duplicate);
                    mergeCount++;
                }
            }
            
            await db.SaveChangesAsync();
            
            return Results.Ok(new { 
                message = $"Merged {mergeCount} duplicate artists",
                duplicateGroupsFound = duplicateGroups.Count
            });
        });

        group.MapPost("/merge-duplicate-albums", async (AudiarrContext db) =>
        {
            var mergeCount = 0;
            
            // Find all duplicate albums by title and artist
            var duplicateGroups = await db.Albums
                .Include(a => a.Artist)
                .GroupBy(a => new { a.Title, a.ArtistId })
                .Where(g => g.Count() > 1)
                .Select(g => new { 
                    Title = g.Key.Title, 
                    ArtistId = g.Key.ArtistId,
                    Albums = g.ToList() 
                })
                .ToListAsync();
            
            foreach (var group in duplicateGroups)
            {
                // Keep the first album, merge all others into it
                var primaryAlbum = group.Albums.First();
                var duplicateAlbums = group.Albums.Skip(1).ToList();
                
                // If primary album doesn't have cover art, try to get it from duplicates
                if (string.IsNullOrEmpty(primaryAlbum.CoverArtPath))
                {
                    var albumWithCover = duplicateAlbums.FirstOrDefault(a => !string.IsNullOrEmpty(a.CoverArtPath));
                    if (albumWithCover != null)
                    {
                        primaryAlbum.CoverArtPath = albumWithCover.CoverArtPath;
                        db.Albums.Update(primaryAlbum);
                    }
                }
                
                foreach (var duplicate in duplicateAlbums)
                {
                    // Update all tracks to point to primary album
                    await db.Tracks
                        .Where(t => t.AlbumId == duplicate.Id)
                        .ExecuteUpdateAsync(t => t.SetProperty(x => x.AlbumId, primaryAlbum.Id));
                    
                    // Remove duplicate album
                    db.Albums.Remove(duplicate);
                    mergeCount++;
                }
            }
            
            await db.SaveChangesAsync();
            
            return Results.Ok(new { 
                message = $"Merged {mergeCount} duplicate albums",
                duplicateGroupsFound = duplicateGroups.Count
            });
        });

        group.MapPost("/clean-all", async ([FromServices] AudiarrContext db) =>
        {
            // First merge duplicate artists
            var artistMergeCount = 0;
            var duplicateArtistGroups = await db.Artists
                .GroupBy(a => a.Name)
                .Where(g => g.Count() > 1)
                .Select(g => new { Name = g.Key, Artists = g.ToList() })
                .ToListAsync();
            
            foreach (var group in duplicateArtistGroups)
            {
                var primaryArtist = group.Artists.First();
                var duplicateArtists = group.Artists.Skip(1).ToList();
                
                foreach (var duplicate in duplicateArtists)
                {
                    await db.Tracks
                        .Where(t => t.ArtistId == duplicate.Id)
                        .ExecuteUpdateAsync(t => t.SetProperty(x => x.ArtistId, primaryArtist.Id));
                    
                    await db.Albums
                        .Where(a => a.ArtistId == duplicate.Id)
                        .ExecuteUpdateAsync(a => a.SetProperty(x => x.ArtistId, primaryArtist.Id));
                    
                    db.Artists.Remove(duplicate);
                    artistMergeCount++;
                }
            }
            
            await db.SaveChangesAsync();
            
            // Then merge duplicate albums
            var albumMergeCount = 0;
            var duplicateAlbumGroups = await db.Albums
                .GroupBy(a => new { a.Title, a.ArtistId })
                .Where(g => g.Count() > 1)
                .Select(g => new { 
                    Title = g.Key.Title, 
                    ArtistId = g.Key.ArtistId,
                    Albums = g.ToList() 
                })
                .ToListAsync();
            
            foreach (var group in duplicateAlbumGroups)
            {
                var primaryAlbum = group.Albums.First();
                var duplicateAlbums = group.Albums.Skip(1).ToList();
                
                if (string.IsNullOrEmpty(primaryAlbum.CoverArtPath))
                {
                    var albumWithCover = duplicateAlbums.FirstOrDefault(a => !string.IsNullOrEmpty(a.CoverArtPath));
                    if (albumWithCover != null)
                    {
                        primaryAlbum.CoverArtPath = albumWithCover.CoverArtPath;
                        db.Albums.Update(primaryAlbum);
                    }
                }
                
                foreach (var duplicate in duplicateAlbums)
                {
                    await db.Tracks
                        .Where(t => t.AlbumId == duplicate.Id)
                        .ExecuteUpdateAsync(t => t.SetProperty(x => x.AlbumId, primaryAlbum.Id));
                    
                    db.Albums.Remove(duplicate);
                    albumMergeCount++;
                }
            }
            
            await db.SaveChangesAsync();
            
            return Results.Ok(new { 
                message = "Database cleanup completed",
                artistsMerged = artistMergeCount,
                albumsMerged = albumMergeCount,
                duplicateArtistGroupsFound = duplicateArtistGroups.Count,
                duplicateAlbumGroupsFound = duplicateAlbumGroups.Count
            });
        });
    }
}