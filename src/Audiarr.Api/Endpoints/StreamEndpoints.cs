using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Audiarr.Data.Context;
using System.Net.Http.Headers;
using System.Text;

namespace Audiarr.Api.Endpoints;

public static class StreamEndpoints
{
    public static void MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v2");

        // Stream audio file with range support
        group.MapGet("/tracks/{id}/stream", async (string id, HttpContext context, AudiarrContext db) =>
        {
            var track = await db.Tracks.FindAsync(id);
            if (track == null)
                return Results.NotFound(new { error = "Track not found" });

            if (string.IsNullOrEmpty(track.FilePath) || !File.Exists(track.FilePath))
                return Results.NotFound(new { error = "Audio file not found" });

            var fileInfo = new FileInfo(track.FilePath);
            var fileLength = fileInfo.Length;
            
            // Determine content type based on file extension
            var contentType = GetAudioContentType(track.FilePath);
            
            // Check if this is a range request
            var rangeHeader = context.Request.Headers.Range.ToString();
            
            if (!string.IsNullOrEmpty(rangeHeader))
            {
                // Parse range header
                var range = RangeHeaderValue.Parse(rangeHeader);
                var ranges = range.Ranges.FirstOrDefault();
                
                if (ranges != null)
                {
                    var start = ranges.From ?? 0;
                    var end = ranges.To ?? fileLength - 1;
                    
                    // Validate range
                    if (start >= fileLength || end >= fileLength)
                    {
                        context.Response.StatusCode = 416; // Range Not Satisfiable
                        context.Response.Headers.ContentRange = $"bytes */{fileLength}";
                        return Results.Empty;
                    }
                    
                    var contentLength = end - start + 1;
                    
                    // Set response headers for partial content
                    context.Response.StatusCode = 206; // Partial Content
                    context.Response.Headers.ContentType = contentType;
                    context.Response.Headers.ContentLength = contentLength;
                    context.Response.Headers.AcceptRanges = "bytes";
                    context.Response.Headers.ContentRange = $"bytes {start}-{end}/{fileLength}";
                    
                    // Stream the requested range
                    using var fileStream = new FileStream(track.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    fileStream.Seek(start, SeekOrigin.Begin);
                    
                    var buffer = new byte[64 * 1024]; // 64KB buffer
                    var bytesRemaining = contentLength;
                    
                    while (bytesRemaining > 0)
                    {
                        var bytesToRead = (int)Math.Min(buffer.Length, bytesRemaining);
                        var bytesRead = await fileStream.ReadAsync(buffer, 0, bytesToRead);
                        
                        if (bytesRead == 0)
                            break;
                            
                        await context.Response.Body.WriteAsync(buffer, 0, bytesRead);
                        bytesRemaining -= bytesRead;
                    }
                    
                    return Results.Empty;
                }
            }
            
            // No range request - return entire file
            context.Response.Headers.AcceptRanges = "bytes";
            return Results.File(track.FilePath, contentType, enableRangeProcessing: true);
        })
        .WithName("StreamTrackAudio")
        .WithOpenApi()
        .WithSummary("Stream audio track")
        .WithDescription("Streams an audio track with support for range requests (seeking)")
        .AllowAnonymous(); // Allow streaming without auth for now

        // Get album play context
        group.MapGet("/albums/{id}/play", async (string id, AudiarrContext db) =>
        {
            var album = await db.Albums
                .Include(a => a.Artist)
                .Include(a => a.Tracks.OrderBy(t => t.DiscNumber).ThenBy(t => t.TrackNumber))
                .FirstOrDefaultAsync(a => a.Id == id);
                
            if (album == null)
                return Results.NotFound(new { error = "Album not found" });
                
            var tracks = album.Tracks.Select((t, index) => new
            {
                t.Id,
                t.Title,
                ArtistName = album.Artist.Name,
                t.TrackNumber,
                t.DiscNumber,
                DurationMs = t.DurationMs,
                StreamUrl = $"/api/v2/tracks/{t.Id}/stream",
                NextTrackId = index < album.Tracks.Count - 1 ? album.Tracks.ElementAt(index + 1).Id : null,
                PreviousTrackId = index > 0 ? album.Tracks.ElementAt(index - 1).Id : null
            }).ToList();
            
            return Results.Ok(new
            {
                album = new
                {
                    album.Id,
                    album.Title,
                    ArtistName = album.Artist.Name,
                    album.Year,
                    album.CoverArtPath,
                    TrackCount = album.Tracks.Count
                },
                tracks,
                totalDurationMs = tracks.Sum(t => t.DurationMs)
            });
        })
        .WithName("GetAlbumPlayContext")
        .WithOpenApi()
        .WithSummary("Get album play context")
        .WithDescription("Returns album information with ordered tracks for continuous playback");

        // Get playlist play context
        group.MapGet("/playlists/{id}/play", [Authorize] async (string id, AudiarrContext db) =>
        {
            var playlist = await db.Playlists
                .Include(p => p.PlaylistTracks)
                    .ThenInclude(pt => pt.Track)
                        .ThenInclude(t => t.Artist)
                .Include(p => p.PlaylistTracks)
                    .ThenInclude(pt => pt.Track)
                        .ThenInclude(t => t.Album)
                .FirstOrDefaultAsync(p => p.Id == id);
                
            if (playlist == null)
                return Results.NotFound(new { error = "Playlist not found" });
                
            var tracks = playlist.PlaylistTracks
                .OrderBy(pt => pt.Position)
                .Select((pt, index) => new
                {
                    pt.Track.Id,
                    pt.Track.Title,
                    ArtistName = pt.Track.Artist.Name,
                    AlbumTitle = pt.Track.Album?.Title,
                    pt.Track.TrackNumber,
                    DurationMs = pt.Track.DurationMs,
                    StreamUrl = $"/api/v2/tracks/{pt.Track.Id}/stream",
                    Position = pt.Position,
                    NextTrackId = index < playlist.PlaylistTracks.Count - 1 
                        ? playlist.PlaylistTracks.OrderBy(x => x.Position).ElementAt(index + 1).TrackId 
                        : null,
                    PreviousTrackId = index > 0 
                        ? playlist.PlaylistTracks.OrderBy(x => x.Position).ElementAt(index - 1).TrackId 
                        : null
                })
                .ToList();
            
            return Results.Ok(new
            {
                playlist = new
                {
                    playlist.Id,
                    playlist.Name,
                    playlist.Description,
                    TrackCount = playlist.PlaylistTracks.Count
                },
                tracks,
                totalDurationMs = tracks.Sum(t => t.DurationMs)
            });
        })
        .WithName("GetPlaylistPlayContext")
        .WithOpenApi()
        .WithSummary("Get playlist play context")
        .WithDescription("Returns playlist information with ordered tracks for continuous playback");

        // Get single track play context
        group.MapGet("/tracks/{id}/play", async (string id, AudiarrContext db) =>
        {
            var track = await db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .FirstOrDefaultAsync(t => t.Id == id);
                
            if (track == null)
                return Results.NotFound(new { error = "Track not found" });
                
            // Find next and previous tracks in the same album if available
            string? nextTrackId = null;
            string? previousTrackId = null;
            
            if (track.AlbumId != null)
            {
                var albumTracks = await db.Tracks
                    .Where(t => t.AlbumId == track.AlbumId)
                    .OrderBy(t => t.DiscNumber)
                    .ThenBy(t => t.TrackNumber)
                    .Select(t => t.Id)
                    .ToListAsync();
                    
                var currentIndex = albumTracks.IndexOf(track.Id);
                if (currentIndex > 0)
                    previousTrackId = albumTracks[currentIndex - 1];
                if (currentIndex < albumTracks.Count - 1)
                    nextTrackId = albumTracks[currentIndex + 1];
            }
            
            return Results.Ok(new
            {
                track = new
                {
                    track.Id,
                    track.Title,
                    ArtistName = track.Artist.Name,
                    AlbumTitle = track.Album?.Title,
                    track.TrackNumber,
                    track.DiscNumber,
                    DurationMs = track.DurationMs,
                    StreamUrl = $"/api/v2/tracks/{track.Id}/stream",
                    track.Genre,
                    track.Year,
                    CoverArtPath = track.Album?.CoverArtPath
                },
                nextTrackId,
                previousTrackId
            });
        })
        .WithName("GetTrackPlayContext")
        .WithOpenApi()
        .WithSummary("Get single track play context")
        .WithDescription("Returns track information with stream URL and navigation links");
    }

    private static string GetAudioContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            ".opus" => "audio/opus",
            ".flac" => "audio/flac",
            ".wav" => "audio/wav",
            ".wma" => "audio/x-ms-wma",
            ".webm" => "audio/webm",
            _ => "application/octet-stream"
        };
    }
}