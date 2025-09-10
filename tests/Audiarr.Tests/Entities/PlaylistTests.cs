using Audiarr.Core.Entities;
using Xunit;

namespace Audiarr.Tests.Entities;

public class PlaylistTests
{
    [Fact]
    public void Playlist_Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var playlist = new Playlist
        {
            Name = "Test Playlist",
            UserId = "user123"
        };

        // Assert
        Assert.Equal("Test Playlist", playlist.Name);
        Assert.Equal("user123", playlist.UserId);
        Assert.False(playlist.IsPublic);
        Assert.Null(playlist.Description);
        Assert.Null(playlist.ImagePath);
        Assert.NotNull(playlist.PlaylistTracks);
        Assert.Empty(playlist.PlaylistTracks);
    }

    [Fact]
    public void Playlist_NewFields_HaveCorrectDefaultValues()
    {
        // Arrange & Act
        var playlist = new Playlist
        {
            Name = "Test Playlist",
            UserId = "user123"
        };

        // Assert - New fields
        Assert.Equal(0, playlist.PlayCount);
        Assert.Equal(0, playlist.TrackCount);
        Assert.Null(playlist.TotalDuration);

        // LastModified should be close to current UTC time (within 1 second)
        var timeDifference = DateTime.UtcNow - playlist.LastModified;
        Assert.True(timeDifference.TotalSeconds < 1,
            $"LastModified should be close to current time. Difference: {timeDifference.TotalSeconds} seconds");
    }

    [Fact]
    public void Playlist_CanSetAndGetAllProperties()
    {
        // Arrange
        var playlist = new Playlist
        {
            Name = "Initial Name",
            UserId = "user123"
        };
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var testDuration = TimeSpan.FromMinutes(45.5);

        // Act
        playlist.Name = "Updated Playlist";
        playlist.Description = "Test Description";
        playlist.UserId = "user456";
        playlist.IsPublic = true;
        playlist.ImagePath = "/images/playlist.jpg";
        playlist.LastModified = testDate;
        playlist.PlayCount = 42;
        playlist.TrackCount = 15;
        playlist.TotalDuration = testDuration;

        // Assert
        Assert.Equal("Updated Playlist", playlist.Name);
        Assert.Equal("Test Description", playlist.Description);
        Assert.Equal("user456", playlist.UserId);
        Assert.True(playlist.IsPublic);
        Assert.Equal("/images/playlist.jpg", playlist.ImagePath);
        Assert.Equal(testDate, playlist.LastModified);
        Assert.Equal(42, playlist.PlayCount);
        Assert.Equal(15, playlist.TrackCount);
        Assert.Equal(testDuration, playlist.TotalDuration);
    }

    [Fact]
    public void Playlist_TotalDuration_CanBeNull()
    {
        // Arrange
        var playlist = new Playlist
        {
            Name = "Test Playlist",
            UserId = "user123"
        };

        // Act & Assert - Default is null
        Assert.Null(playlist.TotalDuration);

        // Can be set to a value
        playlist.TotalDuration = TimeSpan.FromMinutes(30);
        Assert.Equal(TimeSpan.FromMinutes(30), playlist.TotalDuration);

        // Can be set back to null
        playlist.TotalDuration = null;
        Assert.Null(playlist.TotalDuration);
    }

    [Fact]
    public void Playlist_InheritsFromBaseEntity()
    {
        // Arrange & Act
        var playlist = new Playlist
        {
            Name = "Test Playlist",
            UserId = "user123"
        };

        // Assert - BaseEntity properties should exist
        Assert.NotNull(playlist.Id);
        Assert.NotEqual(default(DateTime), playlist.CreatedAt);
        Assert.NotEqual(default(DateTime), playlist.UpdatedAt);

        // Verify it's actually a BaseEntity
        Assert.IsAssignableFrom<BaseEntity>(playlist);
    }

    [Fact]
    public void Playlist_PlaylistTracks_InitializedAsEmptyCollection()
    {
        // Arrange & Act
        var playlist = new Playlist
        {
            Name = "Test Playlist",
            UserId = "user123"
        };

        // Assert
        Assert.NotNull(playlist.PlaylistTracks);
        Assert.IsType<List<PlaylistTrack>>(playlist.PlaylistTracks);
        Assert.Empty(playlist.PlaylistTracks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Playlist_PlayCount_AcceptsValidValues(int playCount)
    {
        // Arrange
        var playlist = new Playlist
        {
            Name = "Test Playlist",
            UserId = "user123"
        };

        // Act
        playlist.PlayCount = playCount;

        // Assert
        Assert.Equal(playCount, playlist.PlayCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void Playlist_TrackCount_AcceptsValidValues(int trackCount)
    {
        // Arrange
        var playlist = new Playlist
        {
            Name = "Test Playlist",
            UserId = "user123"
        };

        // Act
        playlist.TrackCount = trackCount;

        // Assert
        Assert.Equal(trackCount, playlist.TrackCount);
    }

    [Fact]
    public void Playlist_RequiredProperties_MustBeSet()
    {
        // This test verifies that the required modifier works correctly
        // The compiler enforces this, but we can test runtime behavior

        // Arrange & Act
        var playlist = new Playlist
        {
            Name = "Required Name",
            UserId = "required-user"
        };

        // Assert
        Assert.NotNull(playlist.Name);
        Assert.NotNull(playlist.UserId);
    }
}