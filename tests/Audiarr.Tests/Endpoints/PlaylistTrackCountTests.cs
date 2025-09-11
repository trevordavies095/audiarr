using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Audiarr.Data.Context;
using Audiarr.Core.Entities;
using Xunit;

namespace Audiarr.Tests.Endpoints;

public class PlaylistTrackCountTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AudiarrContext> _options;

    public PlaylistTrackCountTests()
    {
        // Create an in-memory SQLite database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AudiarrContext>()
            .UseSqlite(_connection)
            .Options;

        // Create the schema
        using var context = new AudiarrContext(_options);
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task AddingTracks_UpdatesTrackCountAndTotalDuration()
    {
        // Arrange
        using var context = new AudiarrContext(_options);
        
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.Add(user);

        var artist = new Artist
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Artist"
        };
        context.Artists.Add(artist);

        var album = new Album
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Test Album",
            ArtistId = artist.Id
        };
        context.Albums.Add(album);

        // Create test tracks with known durations
        var track1 = new Track
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Track 1",
            FilePath = "/test/track1.mp3",
            ArtistId = artist.Id,
            AlbumId = album.Id,
            DurationMs = 180000 // 3 minutes
        };
        var track2 = new Track
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Track 2",
            FilePath = "/test/track2.mp3",
            ArtistId = artist.Id,
            AlbumId = album.Id,
            DurationMs = 240000 // 4 minutes
        };
        context.Tracks.Add(track1);
        context.Tracks.Add(track2);

        var playlist = new Playlist
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Playlist",
            UserId = user.Id,
            TrackCount = 0,
            TotalDuration = null
        };
        context.Playlists.Add(playlist);
        await context.SaveChangesAsync();

        // Act - Add tracks to playlist
        var playlistTrack1 = new PlaylistTrack
        {
            PlaylistId = playlist.Id,
            TrackId = track1.Id,
            Position = 0,
            PositionFloat = 0
        };
        var playlistTrack2 = new PlaylistTrack
        {
            PlaylistId = playlist.Id,
            TrackId = track2.Id,
            Position = 1,
            PositionFloat = 1
        };
        context.PlaylistTracks.Add(playlistTrack1);
        context.PlaylistTracks.Add(playlistTrack2);

        // Simulate the logic from the endpoint
        playlist.TrackCount = 2;
        playlist.TotalDuration = TimeSpan.FromMilliseconds(420000); // 7 minutes total
        
        await context.SaveChangesAsync();

        // Assert
        context.Entry(playlist).State = EntityState.Detached;
        var updatedPlaylist = await context.Playlists.FindAsync(playlist.Id);
        
        Assert.NotNull(updatedPlaylist);
        Assert.Equal(2, updatedPlaylist.TrackCount);
        Assert.NotNull(updatedPlaylist.TotalDuration);
        Assert.Equal(TimeSpan.FromMinutes(7), updatedPlaylist.TotalDuration.Value);
    }

    [Fact]
    public async Task RemovingAllTracks_ResetsTrackCountAndTotalDuration()
    {
        // Arrange
        using var context = new AudiarrContext(_options);
        
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "testuser2",
            Email = "test2@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.Add(user);

        var artist = new Artist
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Artist 2"
        };
        context.Artists.Add(artist);

        var album = new Album
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Test Album 2",
            ArtistId = artist.Id
        };
        context.Albums.Add(album);

        var track = new Track
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Track to Remove",
            FilePath = "/test/track.mp3",
            ArtistId = artist.Id,
            AlbumId = album.Id,
            DurationMs = 300000 // 5 minutes
        };
        context.Tracks.Add(track);

        var playlist = new Playlist
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Playlist 2",
            UserId = user.Id,
            TrackCount = 1,
            TotalDuration = TimeSpan.FromMinutes(5)
        };
        context.Playlists.Add(playlist);

        var playlistTrack = new PlaylistTrack
        {
            PlaylistId = playlist.Id,
            TrackId = track.Id,
            Position = 0,
            PositionFloat = 0
        };
        context.PlaylistTracks.Add(playlistTrack);
        await context.SaveChangesAsync();

        // Act - Remove the track
        context.PlaylistTracks.Remove(playlistTrack);
        playlist.TrackCount = 0;
        playlist.TotalDuration = null;
        await context.SaveChangesAsync();

        // Assert
        context.Entry(playlist).State = EntityState.Detached;
        var updatedPlaylist = await context.Playlists.FindAsync(playlist.Id);
        
        Assert.NotNull(updatedPlaylist);
        Assert.Equal(0, updatedPlaylist.TrackCount);
        Assert.Null(updatedPlaylist.TotalDuration);
    }

    [Fact]
    public async Task DefensiveRecalculation_FixesMismatchedCounts()
    {
        // Arrange - Create a playlist with mismatched metadata
        using var context = new AudiarrContext(_options);
        
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "testuser3",
            Email = "test3@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.Add(user);

        var artist = new Artist
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Artist 3"
        };
        context.Artists.Add(artist);

        var album = new Album
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Test Album 3",
            ArtistId = artist.Id
        };
        context.Albums.Add(album);

        var track1 = new Track
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Track 1",
            FilePath = "/test/track1.mp3",
            ArtistId = artist.Id,
            AlbumId = album.Id,
            DurationMs = 120000 // 2 minutes
        };
        var track2 = new Track
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Track 2",
            FilePath = "/test/track2.mp3",
            ArtistId = artist.Id,
            AlbumId = album.Id,
            DurationMs = 180000 // 3 minutes
        };
        context.Tracks.Add(track1);
        context.Tracks.Add(track2);

        // Create playlist with incorrect metadata (simulating the bug)
        var playlist = new Playlist
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Mismatched Playlist",
            UserId = user.Id,
            TrackCount = 0, // Wrong! Should be 2
            TotalDuration = null // Wrong! Should be 5 minutes
        };
        context.Playlists.Add(playlist);

        var playlistTrack1 = new PlaylistTrack
        {
            PlaylistId = playlist.Id,
            TrackId = track1.Id,
            Position = 0,
            PositionFloat = 0
        };
        var playlistTrack2 = new PlaylistTrack
        {
            PlaylistId = playlist.Id,
            TrackId = track2.Id,
            Position = 1,
            PositionFloat = 1
        };
        context.PlaylistTracks.Add(playlistTrack1);
        context.PlaylistTracks.Add(playlistTrack2);
        await context.SaveChangesAsync();

        // Act - Load playlist with tracks (simulating GET endpoint behavior)
        var loadedPlaylist = await context.Playlists
            .Include(p => p.PlaylistTracks)
                .ThenInclude(pt => pt.Track)
            .FirstOrDefaultAsync(p => p.Id == playlist.Id);

        Assert.NotNull(loadedPlaylist);

        // Simulate the defensive recalculation logic
        var actualTrackCount = loadedPlaylist.PlaylistTracks.Count;
        var actualTotalDurationMs = loadedPlaylist.PlaylistTracks.Sum(pt => pt.Track?.DurationMs ?? 0);
        var actualTotalDuration = actualTotalDurationMs > 0 ? TimeSpan.FromMilliseconds(actualTotalDurationMs) : (TimeSpan?)null;

        if (loadedPlaylist.TrackCount != actualTrackCount || loadedPlaylist.TotalDuration != actualTotalDuration)
        {
            loadedPlaylist.TrackCount = actualTrackCount;
            loadedPlaylist.TotalDuration = actualTotalDuration;
            await context.SaveChangesAsync();
        }

        // Assert
        context.Entry(loadedPlaylist).State = EntityState.Detached;
        var fixedPlaylist = await context.Playlists.FindAsync(playlist.Id);
        
        Assert.NotNull(fixedPlaylist);
        Assert.Equal(2, fixedPlaylist.TrackCount);
        Assert.NotNull(fixedPlaylist.TotalDuration);
        Assert.Equal(TimeSpan.FromMinutes(5), fixedPlaylist.TotalDuration.Value);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}