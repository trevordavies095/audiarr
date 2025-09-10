using Audiarr.Core.DTOs;
using Xunit;

namespace Audiarr.Tests.DTOs;

public class PlaylistTrackDtoTests
{
    [Fact]
    public void PlaylistTrackDto_Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var dto = new PlaylistTrackDto();

        // Assert
        Assert.Equal(string.Empty, dto.TrackId);
        Assert.Equal(string.Empty, dto.Title);
        Assert.Equal(string.Empty, dto.ArtistId);
        Assert.Equal(string.Empty, dto.ArtistName);
        Assert.Equal(string.Empty, dto.AlbumId);
        Assert.Equal(string.Empty, dto.AlbumTitle);
        Assert.Null(dto.TrackNumber);
        Assert.Null(dto.DiscNumber);
        Assert.Equal(0, dto.DurationMs);
        Assert.Null(dto.Genre);
        Assert.Null(dto.Year);
        Assert.Equal(string.Empty, dto.FilePath);
        Assert.Equal(0, dto.Position);
        Assert.Equal(0m, dto.PositionFloat);
        Assert.Equal(default(DateTime), dto.AddedAt);
        Assert.Null(dto.AddedBy);
    }

    [Fact]
    public void PlaylistTrackDto_CanSetAndGetAllProperties()
    {
        // Arrange
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var dto = new PlaylistTrackDto();

        // Act
        dto.TrackId = "track123";
        dto.Title = "Test Song";
        dto.ArtistId = "artist456";
        dto.ArtistName = "Test Artist";
        dto.AlbumId = "album789";
        dto.AlbumTitle = "Test Album";
        dto.TrackNumber = 5;
        dto.DiscNumber = 1;
        dto.DurationMs = 240000;
        dto.Genre = "Rock";
        dto.Year = 2024;
        dto.FilePath = "/music/test.mp3";
        dto.Position = 3;
        dto.PositionFloat = 3.5m;
        dto.AddedAt = testDate;
        dto.AddedBy = "testuser";

        // Assert
        Assert.Equal("track123", dto.TrackId);
        Assert.Equal("Test Song", dto.Title);
        Assert.Equal("artist456", dto.ArtistId);
        Assert.Equal("Test Artist", dto.ArtistName);
        Assert.Equal("album789", dto.AlbumId);
        Assert.Equal("Test Album", dto.AlbumTitle);
        Assert.Equal(5, dto.TrackNumber);
        Assert.Equal(1, dto.DiscNumber);
        Assert.Equal(240000, dto.DurationMs);
        Assert.Equal("Rock", dto.Genre);
        Assert.Equal(2024, dto.Year);
        Assert.Equal("/music/test.mp3", dto.FilePath);
        Assert.Equal(3, dto.Position);
        Assert.Equal(3.5m, dto.PositionFloat);
        Assert.Equal(testDate, dto.AddedAt);
        Assert.Equal("testuser", dto.AddedBy);
    }

    [Fact]
    public void PlaylistTrackDto_IncludesTrackInfoAndPlaylistInfo()
    {
        // This test verifies that the DTO combines track information with playlist-specific fields
        
        // Arrange & Act
        var dto = new PlaylistTrackDto
        {
            // Track information
            TrackId = "track123",
            Title = "Song Title",
            ArtistName = "Artist Name",
            AlbumTitle = "Album Title",
            DurationMs = 180000,
            
            // Playlist-specific information
            Position = 5,
            PositionFloat = 5.25m,
            AddedAt = DateTime.UtcNow,
            AddedBy = "user123"
        };

        // Assert - Both types of information are present
        Assert.NotEqual(string.Empty, dto.TrackId);
        Assert.NotEqual(string.Empty, dto.Title);
        Assert.NotEqual(0, dto.Position);
        Assert.NotEqual(0m, dto.PositionFloat);
        Assert.NotNull(dto.AddedBy);
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(1, 1.0)]
    [InlineData(1, 1.5)]
    [InlineData(2, 1.75)]
    [InlineData(100, 99.999)]
    public void PlaylistTrackDto_PositionFields_CanHoldDifferentValues(int position, decimal positionFloat)
    {
        // Arrange & Act
        var dto = new PlaylistTrackDto
        {
            Position = position,
            PositionFloat = positionFloat
        };

        // Assert
        Assert.Equal(position, dto.Position);
        Assert.Equal(positionFloat, dto.PositionFloat);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("user123")]
    [InlineData("admin@example.com")]
    public void PlaylistTrackDto_AddedBy_AcceptsVariousValues(string? addedBy)
    {
        // Arrange & Act
        var dto = new PlaylistTrackDto { AddedBy = addedBy };

        // Assert
        Assert.Equal(addedBy, dto.AddedBy);
    }

    [Fact]
    public void PlaylistTrackDto_SupportsReorderingScenario()
    {
        // This test demonstrates how PositionFloat can be used for reordering
        
        // Arrange - Create three tracks in order
        var track1 = new PlaylistTrackDto { TrackId = "1", Position = 0, PositionFloat = 1.0m };
        var track2 = new PlaylistTrackDto { TrackId = "2", Position = 1, PositionFloat = 2.0m };
        var track3 = new PlaylistTrackDto { TrackId = "3", Position = 2, PositionFloat = 3.0m };

        // Act - Move track3 between track1 and track2
        track3.PositionFloat = 1.5m;

        // Assert - track3's PositionFloat is now between track1 and track2
        Assert.True(track3.PositionFloat > track1.PositionFloat);
        Assert.True(track3.PositionFloat < track2.PositionFloat);
    }
}