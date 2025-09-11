using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Audiarr.Data.Context;
using Audiarr.Core.Entities;
using System;
using System.Linq;
using Xunit;

namespace Audiarr.Tests.Data;

public class AudiarrContextTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AudiarrContext> _options;

    public AudiarrContextTests()
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
    public void Playlist_Entity_Configuration_Should_Have_Correct_Indexes()
    {
        using var context = new AudiarrContext(_options);
        var entityType = context.Model.FindEntityType(typeof(Playlist));

        Assert.NotNull(entityType);

        // Check indexes
        var indexes = entityType.GetIndexes().ToList();

        // Verify UserId index exists
        Assert.Contains(indexes, i => i.Properties.Any(p => p.Name == "UserId"));

        // Verify IsPublic index exists
        Assert.Contains(indexes, i => i.Properties.Any(p => p.Name == "IsPublic"));

        // Verify LastModified index exists
        Assert.Contains(indexes, i => i.Properties.Any(p => p.Name == "LastModified"));

        // Verify composite index on UserId and IsPublic exists
        Assert.Contains(indexes, i =>
            i.Properties.Count == 2 &&
            i.Properties.Any(p => p.Name == "UserId") &&
            i.Properties.Any(p => p.Name == "IsPublic"));
    }

    [Fact]
    public void Playlist_Entity_Configuration_Should_Have_Correct_Property_Constraints()
    {
        using var context = new AudiarrContext(_options);
        var entityType = context.Model.FindEntityType(typeof(Playlist));

        Assert.NotNull(entityType);

        // Check Name property
        var nameProperty = entityType.FindProperty("Name");
        Assert.NotNull(nameProperty);
        Assert.False(nameProperty.IsNullable);
        Assert.Equal(100, nameProperty.GetMaxLength());

        // Check Description property
        var descriptionProperty = entityType.FindProperty("Description");
        Assert.NotNull(descriptionProperty);
        Assert.Equal(500, descriptionProperty.GetMaxLength());

        // Check PlayCount default value
        var playCountProperty = entityType.FindProperty("PlayCount");
        Assert.NotNull(playCountProperty);
        Assert.Equal(0, playCountProperty.GetDefaultValue());

        // Check TrackCount default value
        var trackCountProperty = entityType.FindProperty("TrackCount");
        Assert.NotNull(trackCountProperty);
        Assert.Equal(0, trackCountProperty.GetDefaultValue());
    }

    [Fact]
    public void Playlist_Entity_Should_Have_TotalDuration_Conversion()
    {
        using var context = new AudiarrContext(_options);
        var entityType = context.Model.FindEntityType(typeof(Playlist));

        var totalDurationProperty = entityType?.FindProperty("TotalDuration");
        Assert.NotNull(totalDurationProperty);

        // Verify that a value converter is configured
        var converter = totalDurationProperty.GetValueConverter();
        Assert.NotNull(converter);
    }

    [Fact]
    public void PlaylistTrack_Entity_Configuration_Should_Have_Correct_Indexes()
    {
        using var context = new AudiarrContext(_options);
        var entityType = context.Model.FindEntityType(typeof(PlaylistTrack));

        Assert.NotNull(entityType);

        // Check indexes
        var indexes = entityType.GetIndexes().ToList();

        // Verify composite index on PlaylistId and Position exists
        Assert.Contains(indexes, i =>
            i.Properties.Count == 2 &&
            i.Properties.Any(p => p.Name == "PlaylistId") &&
            i.Properties.Any(p => p.Name == "Position"));

        // Verify composite index on PlaylistId and PositionFloat exists
        Assert.Contains(indexes, i =>
            i.Properties.Count == 2 &&
            i.Properties.Any(p => p.Name == "PlaylistId") &&
            i.Properties.Any(p => p.Name == "PositionFloat"));

        // Verify AddedAt index exists
        Assert.Contains(indexes, i => i.Properties.Any(p => p.Name == "AddedAt"));
    }

    [Fact]
    public void PlaylistTrack_Entity_Configuration_Should_Have_Correct_Property_Constraints()
    {
        using var context = new AudiarrContext(_options);
        var entityType = context.Model.FindEntityType(typeof(PlaylistTrack));

        Assert.NotNull(entityType);

        // Check PositionFloat is double type (no precision needed)
        var positionFloatProperty = entityType.FindProperty("PositionFloat");
        Assert.NotNull(positionFloatProperty);
        Assert.Equal(typeof(double), positionFloatProperty.ClrType);

        // Check AddedBy max length
        var addedByProperty = entityType.FindProperty("AddedBy");
        Assert.NotNull(addedByProperty);
        Assert.Equal(50, addedByProperty.GetMaxLength());

        // Check AddedAt default value
        var addedAtProperty = entityType.FindProperty("AddedAt");
        Assert.NotNull(addedAtProperty);
        var defaultValueSql = addedAtProperty.GetDefaultValueSql();
        Assert.Equal("CURRENT_TIMESTAMP", defaultValueSql);
    }

    [Fact]
    public void Playlist_Should_Cascade_Delete_PlaylistTracks()
    {
        using var context = new AudiarrContext(_options);

        // Create a user
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.Add(user);

        // Create an artist and album
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

        // Create a track
        var track = new Track
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Test Track",
            FilePath = "/test/track.mp3",
            ArtistId = artist.Id,
            AlbumId = album.Id
        };
        context.Tracks.Add(track);

        // Create a playlist
        var playlist = new Playlist
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Playlist",
            UserId = user.Id
        };
        context.Playlists.Add(playlist);

        // Add track to playlist
        var playlistTrack = new PlaylistTrack
        {
            PlaylistId = playlist.Id,
            TrackId = track.Id,
            Position = 0,
            PositionFloat = 0
        };
        context.PlaylistTracks.Add(playlistTrack);

        context.SaveChanges();

        // Verify the playlist track exists
        Assert.Single(context.PlaylistTracks);

        // Delete the playlist
        context.Playlists.Remove(playlist);
        context.SaveChanges();

        // Verify the playlist track was cascade deleted
        Assert.Empty(context.PlaylistTracks);
    }

    [Fact]
    public void Track_Deletion_Should_Cascade_Delete_PlaylistTracks()
    {
        using var context = new AudiarrContext(_options);

        // Create a user
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "testuser2",
            Email = "test2@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.Add(user);

        // Create an artist and album
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

        // Create a track
        var track = new Track
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Test Track 2",
            FilePath = "/test/track2.mp3",
            ArtistId = artist.Id,
            AlbumId = album.Id
        };
        context.Tracks.Add(track);

        // Create a playlist
        var playlist = new Playlist
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Playlist 2",
            UserId = user.Id
        };
        context.Playlists.Add(playlist);

        // Add track to playlist
        var playlistTrack = new PlaylistTrack
        {
            PlaylistId = playlist.Id,
            TrackId = track.Id,
            Position = 0,
            PositionFloat = 0
        };
        context.PlaylistTracks.Add(playlistTrack);

        context.SaveChanges();

        // Verify the playlist track exists
        Assert.Single(context.PlaylistTracks);

        // Delete the track
        context.Tracks.Remove(track);
        context.SaveChanges();

        // Verify the playlist track was cascade deleted
        Assert.Empty(context.PlaylistTracks);
    }

    [Fact]
    public void Playlist_Default_Values_Should_Be_Applied()
    {
        using var context = new AudiarrContext(_options);

        // Create a user
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "testuser3",
            Email = "test3@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.Add(user);
        context.SaveChanges();

        // Create a playlist without setting PlayCount and TrackCount
        var playlist = new Playlist
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Playlist 3",
            UserId = user.Id
        };
        context.Playlists.Add(playlist);
        context.SaveChanges();

        // Reload the playlist from database
        context.Entry(playlist).State = EntityState.Detached;
        var reloadedPlaylist = context.Playlists.Find(playlist.Id);

        Assert.NotNull(reloadedPlaylist);
        Assert.Equal(0, reloadedPlaylist.PlayCount);
        Assert.Equal(0, reloadedPlaylist.TrackCount);
    }

    [Fact]
    public void TotalDuration_Should_Store_And_Retrieve_Correctly()
    {
        using var context = new AudiarrContext(_options);

        // Create a user
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "testuser4",
            Email = "test4@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.Add(user);

        // Create a playlist with TotalDuration
        var expectedDuration = TimeSpan.FromMinutes(42.5);
        var playlist = new Playlist
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Playlist 4",
            UserId = user.Id,
            TotalDuration = expectedDuration
        };
        context.Playlists.Add(playlist);
        context.SaveChanges();

        // Reload the playlist from database
        context.Entry(playlist).State = EntityState.Detached;
        var reloadedPlaylist = context.Playlists.Find(playlist.Id);

        Assert.NotNull(reloadedPlaylist);
        Assert.NotNull(reloadedPlaylist.TotalDuration);
        Assert.Equal(expectedDuration, reloadedPlaylist.TotalDuration.Value);
    }

    [Fact]
    public void TotalDuration_Should_Handle_Null_Values()
    {
        using var context = new AudiarrContext(_options);

        // Create a user
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "testuser5",
            Email = "test5@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.Add(user);

        // Create a playlist without TotalDuration
        var playlist = new Playlist
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Playlist 5",
            UserId = user.Id,
            TotalDuration = null
        };
        context.Playlists.Add(playlist);
        context.SaveChanges();

        // Reload the playlist from database
        context.Entry(playlist).State = EntityState.Detached;
        var reloadedPlaylist = context.Playlists.Find(playlist.Id);

        Assert.NotNull(reloadedPlaylist);
        Assert.Null(reloadedPlaylist.TotalDuration);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}