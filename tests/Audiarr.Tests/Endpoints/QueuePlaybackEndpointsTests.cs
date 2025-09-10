using Audiarr.Core.DTOs.Requests;
using Audiarr.Core.Entities;
using Xunit;

namespace Audiarr.Tests.Endpoints;

public class QueuePlaybackEndpointsTests
{
    // Test Queue Request DTOs validation

    [Fact]
    public void AddToQueueRequest_Validation_Works()
    {
        // Arrange
        var validRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2" },
            Source = "playlist",
            PlayNext = true
        };

        // Act & Assert
        Assert.NotNull(validRequest.TrackIds);
        Assert.Equal(2, validRequest.TrackIds.Count);
        Assert.Contains("track-1", validRequest.TrackIds);
        Assert.Contains("track-2", validRequest.TrackIds);
        Assert.Equal("playlist", validRequest.Source);
        Assert.True(validRequest.PlayNext);
    }

    [Fact]
    public void AddToQueueRequest_Defaults_Work()
    {
        // Arrange
        var request = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1" }
        };

        // Act & Assert
        Assert.False(request.PlayNext);
        Assert.Null(request.Source);
    }

    [Fact]
    public void ReorderQueueRequest_Validation_Works()
    {
        // Arrange
        var validRequest = new ReorderQueueRequest
        {
            TrackId = "track-1",
            NewIndex = 5
        };

        // Act & Assert
        Assert.Equal("track-1", validRequest.TrackId);
        Assert.Equal(5, validRequest.NewIndex);
    }

    [Fact]
    public void UpdateQueueRequest_Validation_Works()
    {
        // Arrange
        var validRequest = new UpdateQueueRequest
        {
            RepeatMode = RepeatMode.All,
            IsShuffled = true,
            CurrentIndex = 3
        };

        // Act & Assert
        Assert.Equal(RepeatMode.All, validRequest.RepeatMode);
        Assert.True(validRequest.IsShuffled);
        Assert.Equal(3, validRequest.CurrentIndex);
    }

    [Fact]
    public void UpdateQueueRequest_Nullable_Properties_Work()
    {
        // Arrange
        var request = new UpdateQueueRequest();

        // Act & Assert
        // All properties should be nullable and default to null
        Assert.Null(request.CurrentIndex);
        Assert.Null(request.RepeatMode);
        Assert.Null(request.IsShuffled);
    }

    [Fact]
    public void RepeatMode_Enum_Values_Are_Correct()
    {
        // Assert
        Assert.Equal(0, (int)RepeatMode.None);
        Assert.Equal(1, (int)RepeatMode.One);
        Assert.Equal(2, (int)RepeatMode.All);
    }

    [Fact]
    public void ClearQueueRequest_Default_Values_Work()
    {
        // Arrange
        var request = new ClearQueueRequest();

        // Act & Assert
        Assert.False(request.KeepCurrentTrack);
    }

    [Fact]
    public void ClearQueueRequest_KeepCurrentTrack_Works()
    {
        // Arrange
        var request = new ClearQueueRequest
        {
            KeepCurrentTrack = true
        };

        // Act & Assert
        Assert.True(request.KeepCurrentTrack);
    }

    [Fact]
    public void ReplaceQueueRequest_Validation_Works()
    {
        // Arrange
        var validRequest = new ReplaceQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3" },
            StartIndex = 1,
            Source = "album"
        };

        // Act & Assert
        Assert.NotNull(validRequest.TrackIds);
        Assert.Equal(3, validRequest.TrackIds.Count);
        Assert.Equal(1, validRequest.StartIndex);
        Assert.Equal("album", validRequest.Source);
    }

    [Fact]
    public void ReplaceQueueRequest_Default_StartIndex_Is_Zero()
    {
        // Arrange
        var request = new ReplaceQueueRequest
        {
            TrackIds = new List<string> { "track-1" }
        };

        // Act & Assert
        Assert.Equal(0, request.StartIndex);
        Assert.Null(request.Source);
    }

    [Fact]
    public void AddToQueueRequest_With_Max_Tracks_Works()
    {
        // Arrange
        var trackIds = new List<string>();
        for (int i = 1; i <= 100; i++)
        {
            trackIds.Add($"track-{i}");
        }

        var request = new AddToQueueRequest
        {
            TrackIds = trackIds
        };

        // Act & Assert
        Assert.Equal(100, request.TrackIds.Count);
    }

    [Fact]
    public void ReplaceQueueRequest_With_Many_Tracks_Works()
    {
        // Arrange
        var trackIds = new List<string>();
        for (int i = 1; i <= 1000; i++)
        {
            trackIds.Add($"track-{i}");
        }

        var request = new ReplaceQueueRequest
        {
            TrackIds = trackIds
        };

        // Act & Assert
        Assert.Equal(1000, request.TrackIds.Count);
    }

    [Fact]
    public void UpdateQueueRequest_With_Only_RepeatMode_Works()
    {
        // Arrange
        var request = new UpdateQueueRequest
        {
            RepeatMode = RepeatMode.One
        };

        // Act & Assert
        Assert.Equal(RepeatMode.One, request.RepeatMode);
        Assert.Null(request.IsShuffled);
        Assert.Null(request.CurrentIndex);
    }

    [Fact]
    public void UpdateQueueRequest_With_Only_Shuffle_Works()
    {
        // Arrange
        var request = new UpdateQueueRequest
        {
            IsShuffled = false
        };

        // Act & Assert
        Assert.False(request.IsShuffled);
        Assert.Null(request.RepeatMode);
        Assert.Null(request.CurrentIndex);
    }
}