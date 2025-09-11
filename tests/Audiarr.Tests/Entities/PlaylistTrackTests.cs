using Audiarr.Core.Entities;
using Xunit;

namespace Audiarr.Tests.Entities;

public class PlaylistTrackTests
{
    [Fact]
    public void PlaylistTrack_Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var playlistTrack = new PlaylistTrack
        {
            PlaylistId = "playlist123",
            TrackId = "track456"
        };

        // Assert
        Assert.Equal("playlist123", playlistTrack.PlaylistId);
        Assert.Equal("track456", playlistTrack.TrackId);
        Assert.Equal(0, playlistTrack.Position);
        Assert.Equal(0, playlistTrack.PositionFloat);
        Assert.Null(playlistTrack.AddedBy);

        // AddedAt should be close to current UTC time (within 1 second)
        var timeDifference = DateTime.UtcNow - playlistTrack.AddedAt;
        Assert.True(timeDifference.TotalSeconds < 1,
            $"AddedAt should be close to current time. Difference: {timeDifference.TotalSeconds} seconds");
    }

    [Fact]
    public void PlaylistTrack_PositionFloat_DefaultsToZero()
    {
        // Arrange & Act
        var playlistTrack = new PlaylistTrack
        {
            PlaylistId = "playlist123",
            TrackId = "track456"
        };

        // Assert
        Assert.Equal(0, playlistTrack.PositionFloat);
        Assert.IsType<double>(playlistTrack.PositionFloat);
    }

    [Fact]
    public void PlaylistTrack_AddedBy_IsNullableString()
    {
        // Arrange
        var playlistTrack = new PlaylistTrack
        {
            PlaylistId = "playlist123",
            TrackId = "track456"
        };

        // Act & Assert - Default is null
        Assert.Null(playlistTrack.AddedBy);

        // Can be set to a value
        playlistTrack.AddedBy = "user123";
        Assert.Equal("user123", playlistTrack.AddedBy);

        // Can be set back to null
        playlistTrack.AddedBy = null;
        Assert.Null(playlistTrack.AddedBy);
    }

    [Fact]
    public void PlaylistTrack_CanSetAndGetAllProperties()
    {
        // Arrange
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        var playlistTrack = new PlaylistTrack
        {
            PlaylistId = "playlist123",
            TrackId = "track456"
        };

        // Act
        playlistTrack.Position = 5;
        playlistTrack.PositionFloat = 5.5;
        playlistTrack.AddedAt = testDate;
        playlistTrack.AddedBy = "testuser";

        // Assert
        Assert.Equal("playlist123", playlistTrack.PlaylistId);
        Assert.Equal("track456", playlistTrack.TrackId);
        Assert.Equal(5, playlistTrack.Position);
        Assert.Equal(5.5, playlistTrack.PositionFloat);
        Assert.Equal(testDate, playlistTrack.AddedAt);
        Assert.Equal("testuser", playlistTrack.AddedBy);
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(1, 1.0)]
    [InlineData(1, 1.5)]
    [InlineData(2, 1.75)]
    [InlineData(100, 99.999)]
    [InlineData(int.MaxValue, 2147483647.5)]
    public void PlaylistTrack_PositionFields_CanHoldDifferentValues(int position, double positionFloat)
    {
        // Arrange
        var playlistTrack = new PlaylistTrack
        {
            PlaylistId = "playlist123",
            TrackId = "track456"
        };

        // Act
        playlistTrack.Position = position;
        playlistTrack.PositionFloat = positionFloat;

        // Assert
        Assert.Equal(position, playlistTrack.Position);
        Assert.Equal(positionFloat, playlistTrack.PositionFloat);
    }

    [Fact]
    public void PlaylistTrack_PositionFloat_SupportsReorderingBetweenItems()
    {
        // This test demonstrates the use case for PositionFloat
        // Arrange
        var track1 = new PlaylistTrack
        {
            PlaylistId = "playlist123",
            TrackId = "track1",
            Position = 1,
            PositionFloat = 1.0
        };

        var track2 = new PlaylistTrack
        {
            PlaylistId = "playlist123",
            TrackId = "track2",
            Position = 2,
            PositionFloat = 2.0
        };

        // Act - Insert a track between track1 and track2
        var insertedTrack = new PlaylistTrack
        {
            PlaylistId = "playlist123",
            TrackId = "track3",
            Position = 1, // Could still be 1, but...
            PositionFloat = 1.5 // This allows precise positioning
        };

        // Assert - The inserted track's PositionFloat is between the other two
        Assert.True(insertedTrack.PositionFloat > track1.PositionFloat);
        Assert.True(insertedTrack.PositionFloat < track2.PositionFloat);
    }

    [Fact]
    public void PlaylistTrack_NavigationProperties_InitiallyNull()
    {
        // Arrange & Act
        var playlistTrack = new PlaylistTrack
        {
            PlaylistId = "playlist123",
            TrackId = "track456"
        };

        // Assert - Navigation properties should be null! initially
        // The null! annotation tells the compiler they won't be null at runtime
        // when properly loaded from the database
        Assert.Null(playlistTrack.Playlist!);
        Assert.Null(playlistTrack.Track!);
    }

    [Fact]
    public void PlaylistTrack_RequiredProperties_MustBeSet()
    {
        // This test verifies that PlaylistId and TrackId are required
        // The compiler enforces this, but we can test runtime behavior

        // Arrange & Act
        var playlistTrack = new PlaylistTrack
        {
            PlaylistId = "required-playlist",
            TrackId = "required-track"
        };

        // Assert
        Assert.NotNull(playlistTrack.PlaylistId);
        Assert.NotNull(playlistTrack.TrackId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("u")]
    [InlineData("user123")]
    [InlineData("very_long_username_that_exceeds_normal_length_limits_to_test_field_capacity")]
    public void PlaylistTrack_AddedBy_AcceptsVariousStringValues(string addedBy)
    {
        // Arrange
        var playlistTrack = new PlaylistTrack
        {
            PlaylistId = "playlist123",
            TrackId = "track456"
        };

        // Act
        playlistTrack.AddedBy = addedBy;

        // Assert
        Assert.Equal(addedBy, playlistTrack.AddedBy);
    }

    [Fact]
    public void PlaylistTrack_PositionFloat_PrecisionForMultipleInsertions()
    {
        // This test demonstrates how PositionFloat can handle multiple insertions
        // without needing to reindex

        // Arrange - Start with two tracks
        double position1 = 1.0;
        double position2 = 2.0;

        // Act - Simulate multiple insertions between them
        var positions = new List<double>();
        for (int i = 0; i < 10; i++)
        {
            var newPosition = (position1 + position2) / 2;
            positions.Add(newPosition);
            position2 = newPosition; // Next insertion will be between position1 and this new position
        }

        // Assert - All positions are unique and between 1.0 and 2.0
        Assert.Equal(10, positions.Count);
        Assert.All(positions, p => Assert.True(p > 1.0 && p < 2.0));
        Assert.Equal(positions.Count, positions.Distinct().Count()); // All unique

        // Verify they're in descending order (each new one is closer to 1.0)
        for (int i = 1; i < positions.Count; i++)
        {
            Assert.True(positions[i] < positions[i - 1]);
        }
    }
}