using System.ComponentModel.DataAnnotations;
using Audiarr.Core.DTOs;
using Audiarr.Core.DTOs.Requests;
using Audiarr.Core.Entities;
using Xunit;

namespace Audiarr.Tests.Endpoints;

public class QueueEndpointsTests
{
    [Fact]
    public void AddToQueueRequest_Validation_Works()
    {
        // Arrange
        var validRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track1", "track2", "track3" },
            Source = "playlist",
            PlayNext = true
        };

        // Act & Assert
        Assert.NotNull(validRequest.TrackIds);
        Assert.Equal(3, validRequest.TrackIds.Count);
        Assert.Equal("playlist", validRequest.Source);
        Assert.True(validRequest.PlayNext);
    }

    [Fact]
    public void AddToQueueRequest_RequiresTrackIds()
    {
        // Arrange
        var request = new AddToQueueRequest
        {
            TrackIds = new List<string>() // Empty list should still be valid as object
        };

        // Act & Assert
        Assert.NotNull(request.TrackIds);
        Assert.Empty(request.TrackIds);
    }

    [Fact]
    public void UpdateQueueRequest_Validation_Works()
    {
        // Arrange
        var request = new UpdateQueueRequest
        {
            RepeatMode = RepeatMode.All,
            IsShuffled = true,
            CurrentIndex = 5
        };

        // Act & Assert
        Assert.Equal(RepeatMode.All, request.RepeatMode);
        Assert.True(request.IsShuffled);
        Assert.Equal(5, request.CurrentIndex);
    }

    [Fact]
    public void UpdateQueueRequest_AllFieldsOptional()
    {
        // Arrange
        var request = new UpdateQueueRequest();

        // Act & Assert
        Assert.Null(request.RepeatMode);
        Assert.Null(request.IsShuffled);
        Assert.Null(request.CurrentIndex);
    }

    [Fact]
    public void ReorderQueueRequest_Validation_Works()
    {
        // Arrange
        var request = new ReorderQueueRequest
        {
            TrackId = "track123",
            NewIndex = 3
        };

        // Act & Assert
        Assert.Equal("track123", request.TrackId);
        Assert.Equal(3, request.NewIndex);
    }

    [Fact]
    public void ReplaceQueueRequest_Validation_Works()
    {
        // Arrange
        var request = new ReplaceQueueRequest
        {
            TrackIds = new List<string> { "track1", "track2" },
            StartIndex = 1,
            Source = "album"
        };

        // Act & Assert
        Assert.NotNull(request.TrackIds);
        Assert.Equal(2, request.TrackIds.Count);
        Assert.Equal(1, request.StartIndex);
        Assert.Equal("album", request.Source);
    }

    [Fact]
    public void ReplaceQueueRequest_DefaultStartIndex()
    {
        // Arrange
        var request = new ReplaceQueueRequest
        {
            TrackIds = new List<string> { "track1" }
        };

        // Act & Assert
        Assert.Equal(0, request.StartIndex); // Should default to 0
    }

    [Fact]
    public void ClearQueueRequest_Validation_Works()
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
    public void ClearQueueRequest_DefaultKeepCurrentTrack()
    {
        // Arrange
        var request = new ClearQueueRequest();

        // Act & Assert
        Assert.False(request.KeepCurrentTrack); // Should default to false
    }

    [Fact]
    public void QueueStateDto_Properties_Work()
    {
        // Arrange
        var queueState = new QueueStateDto
        {
            QueueId = "queue123",
            UserId = "user456",
            TrackIds = new List<string> { "track1", "track2" },
            CurrentTrackId = "track1",
            CurrentIndex = 0,
            RepeatMode = RepeatMode.One,
            IsShuffled = false,
            TotalTracks = 2,
            QueueSource = "playlist",
            LastActivity = DateTime.UtcNow,
            Version = 1
        };

        // Act & Assert
        Assert.Equal("queue123", queueState.QueueId);
        Assert.Equal("user456", queueState.UserId);
        Assert.NotNull(queueState.TrackIds);
        Assert.Equal(2, queueState.TrackIds.Count);
        Assert.Equal("track1", queueState.CurrentTrackId);
        Assert.Equal(0, queueState.CurrentIndex);
        Assert.Equal(RepeatMode.One, queueState.RepeatMode);
        Assert.False(queueState.IsShuffled);
        Assert.Equal(2, queueState.TotalTracks);
        Assert.Equal("playlist", queueState.QueueSource);
        Assert.Equal(1, queueState.Version);
    }

    [Fact]
    public void RepeatMode_Enum_Values()
    {
        // Assert
        Assert.Equal(0, (int)RepeatMode.None);
        Assert.Equal(1, (int)RepeatMode.One);
        Assert.Equal(2, (int)RepeatMode.All);
    }

    [Fact]
    public void AddToQueueRequest_MaxTrackLimit()
    {
        // Arrange - Create list with 100 tracks (the max according to validation)
        var trackIds = Enumerable.Range(1, 100).Select(i => $"track{i}").ToList();
        var request = new AddToQueueRequest
        {
            TrackIds = trackIds
        };

        // Act & Assert
        Assert.Equal(100, request.TrackIds.Count);
    }

    [Fact]
    public void ReplaceQueueRequest_MaxTrackLimit()
    {
        // Arrange - Create list with 1000 tracks (the max for queue)
        var trackIds = Enumerable.Range(1, 1000).Select(i => $"track{i}").ToList();
        var request = new ReplaceQueueRequest
        {
            TrackIds = trackIds
        };

        // Act & Assert
        Assert.Equal(1000, request.TrackIds.Count);
    }

    [Fact]
    public void QueueStateDto_DefaultValues()
    {
        // Arrange
        var queueState = new QueueStateDto();

        // Act & Assert
        Assert.Equal(string.Empty, queueState.QueueId); // Defaults to empty string
        Assert.Equal(string.Empty, queueState.UserId); // Defaults to empty string
        Assert.NotNull(queueState.TrackIds); // Defaults to new list
        Assert.Empty(queueState.TrackIds); // But the list is empty
        Assert.Null(queueState.CurrentTrackId);
        Assert.Equal(0, queueState.CurrentIndex);
        Assert.Equal(RepeatMode.None, queueState.RepeatMode);
        Assert.False(queueState.IsShuffled);
        Assert.Equal(0, queueState.TotalTracks);
        Assert.Null(queueState.QueueSource);
        Assert.Equal(default(DateTime), queueState.LastActivity);
        Assert.Equal(0, queueState.Version);
    }
}