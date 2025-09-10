using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Audiarr.Core.Entities;
using Xunit;

namespace Audiarr.Tests.Entities;

public class PlaybackQueueTests
{
    #region Basic Property Tests

    [Fact]
    public void PlaybackQueue_Should_Have_Default_Values()
    {
        // Arrange & Act
        var queue = new PlaybackQueue { UserId = "user123" };

        // Assert
        Assert.NotNull(queue.Id);
        Assert.Equal("user123", queue.UserId);
        Assert.Equal("{}", queue.QueueStateJson);
        Assert.Equal(0, queue.CurrentIndex);
        Assert.Null(queue.CurrentTrackId);
        Assert.Equal(RepeatMode.None, queue.RepeatMode);
        Assert.False(queue.IsShuffled);
        Assert.Equal(1, queue.Version);
        Assert.NotEqual(default(DateTime), queue.LastActivity);
    }

    [Fact]
    public void PlaybackQueue_Should_Set_All_Properties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        
        // Act
        var queue = new PlaybackQueue
        {
            UserId = "user456",
            QueueStateJson = "{\"trackIds\":[\"t1\",\"t2\"]}",
            CurrentIndex = 1,
            CurrentTrackId = "t2",
            RepeatMode = RepeatMode.All,
            IsShuffled = true,
            LastActivity = now,
            Version = 2
        };

        // Assert
        Assert.Equal("user456", queue.UserId);
        Assert.Equal("{\"trackIds\":[\"t1\",\"t2\"]}", queue.QueueStateJson);
        Assert.Equal(1, queue.CurrentIndex);
        Assert.Equal("t2", queue.CurrentTrackId);
        Assert.Equal(RepeatMode.All, queue.RepeatMode);
        Assert.True(queue.IsShuffled);
        Assert.Equal(now, queue.LastActivity);
        Assert.Equal(2, queue.Version);
    }

    #endregion

    #region QueueState Property Tests

    [Fact]
    public void QueueState_Should_Deserialize_From_JSON()
    {
        // Arrange
        var queue = new PlaybackQueue
        {
            UserId = "user123",
            QueueStateJson = "{\"TrackIds\":[\"track1\",\"track2\"],\"OriginalTrackIds\":[\"track1\",\"track2\"]}"
        };

        // Act
        var state = queue.QueueState;

        // Assert
        Assert.NotNull(state);
        Assert.NotNull(state.TrackIds);
        Assert.Equal(2, state.TrackIds.Count);
        Assert.Contains("track1", state.TrackIds);
        Assert.Contains("track2", state.TrackIds);
    }

    [Fact]
    public void QueueState_Should_Serialize_To_JSON()
    {
        // Arrange
        var queue = new PlaybackQueue { UserId = "user123" };
        var state = new QueueState
        {
            TrackIds = new List<string> { "track1", "track2", "track3" },
            OriginalTrackIds = new List<string> { "track1", "track2", "track3" }
        };

        // Act
        queue.QueueState = state;

        // Assert
        Assert.Contains("track1", queue.QueueStateJson);
        Assert.Contains("track2", queue.QueueStateJson);
        Assert.Contains("track3", queue.QueueStateJson);
        
        // Verify it can be deserialized back
        var deserializedState = JsonSerializer.Deserialize<QueueState>(queue.QueueStateJson);
        Assert.NotNull(deserializedState);
        Assert.Equal(3, deserializedState.TrackIds?.Count);
    }

    [Fact]
    public void QueueState_Should_Cache_Deserialized_Object()
    {
        // Arrange
        var queue = new PlaybackQueue
        {
            UserId = "user123",
            QueueStateJson = "{\"TrackIds\":[\"track1\"]}"
        };

        // Act
        var state1 = queue.QueueState;
        var state2 = queue.QueueState;

        // Assert - should be same instance
        Assert.Same(state1, state2);
    }

    #endregion

    #region Helper Method Tests

    [Fact]
    public void UpdateActivity_Should_Update_Timestamps()
    {
        // Arrange
        var queue = new PlaybackQueue { UserId = "user123" };
        var initialActivity = queue.LastActivity;
        var initialUpdated = queue.UpdatedAt;

        // Wait a bit to ensure time difference
        System.Threading.Thread.Sleep(10);

        // Act
        queue.UpdateActivity();

        // Assert
        Assert.True(queue.LastActivity > initialActivity);
        Assert.True(queue.UpdatedAt > initialUpdated);
    }

    [Fact]
    public void HasTracks_Should_Return_Correct_Value()
    {
        // Arrange
        var emptyQueue = new PlaybackQueue { UserId = "user123" };
        var queueWithTracks = new PlaybackQueue
        {
            UserId = "user456",
            QueueStateJson = "{\"TrackIds\":[\"track1\",\"track2\"]}"
        };

        // Act & Assert
        Assert.False(emptyQueue.HasTracks());
        Assert.True(queueWithTracks.HasTracks());
    }

    [Fact]
    public void GetTrackCount_Should_Return_Correct_Count()
    {
        // Arrange
        var emptyQueue = new PlaybackQueue { UserId = "user123" };
        var queueWithTracks = new PlaybackQueue
        {
            UserId = "user456",
            QueueStateJson = "{\"TrackIds\":[\"track1\",\"track2\",\"track3\"]}"
        };

        // Act & Assert
        Assert.Equal(0, emptyQueue.GetTrackCount());
        Assert.Equal(3, queueWithTracks.GetTrackCount());
    }

    [Fact]
    public void ClearQueue_Should_Reset_All_Values()
    {
        // Arrange
        var queue = new PlaybackQueue
        {
            UserId = "user123",
            QueueStateJson = "{\"TrackIds\":[\"track1\",\"track2\"]}",
            CurrentIndex = 1,
            CurrentTrackId = "track2",
            IsShuffled = true
        };

        // Act
        queue.ClearQueue();

        // Assert
        Assert.NotNull(queue.QueueState);
        Assert.NotNull(queue.QueueState.TrackIds);
        Assert.Empty(queue.QueueState.TrackIds);
        Assert.Equal(0, queue.CurrentIndex);
        Assert.Null(queue.CurrentTrackId);
        Assert.False(queue.IsShuffled);
    }

    [Fact]
    public void SetTracks_Should_Set_Tracks_Without_Shuffle()
    {
        // Arrange
        var queue = new PlaybackQueue { UserId = "user123" };
        var tracks = new List<string> { "track1", "track2", "track3" };

        // Act
        queue.SetTracks(tracks, shuffle: false);

        // Assert
        Assert.NotNull(queue.QueueState.TrackIds);
        Assert.Equal(3, queue.QueueState.TrackIds.Count);
        Assert.Equal("track1", queue.QueueState.TrackIds[0]);
        Assert.Equal("track2", queue.QueueState.TrackIds[1]);
        Assert.Equal("track3", queue.QueueState.TrackIds[2]);
        Assert.Equal(0, queue.CurrentIndex);
        Assert.Equal("track1", queue.CurrentTrackId);
        Assert.False(queue.IsShuffled);
        Assert.Null(queue.QueueState.ShuffledTrackIds);
    }

    [Fact]
    public void SetTracks_Should_Set_Tracks_With_Shuffle()
    {
        // Arrange
        var queue = new PlaybackQueue { UserId = "user123" };
        var tracks = new List<string> { "track1", "track2", "track3", "track4", "track5" };

        // Act
        queue.SetTracks(tracks, shuffle: true);

        // Assert
        Assert.Equal(5, queue.QueueState.TrackIds?.Count);
        Assert.Equal(5, queue.QueueState.OriginalTrackIds?.Count);
        Assert.Equal(5, queue.QueueState.ShuffledTrackIds?.Count);
        Assert.True(queue.IsShuffled);
        
        // Verify shuffled list contains all tracks
        Assert.NotNull(queue.QueueState.ShuffledTrackIds);
        foreach (var track in tracks)
        {
            Assert.Contains(track, queue.QueueState.ShuffledTrackIds);
        }
        
        // Verify original order is preserved
        Assert.Equal(tracks, queue.QueueState.OriginalTrackIds);
    }

    [Fact]
    public void SetTracks_Should_Throw_When_Exceeding_Limit()
    {
        // Arrange
        var queue = new PlaybackQueue { UserId = "user123" };
        var tooManyTracks = Enumerable.Range(1, 1001).Select(i => $"track{i}").ToList();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => queue.SetTracks(tooManyTracks));
        Assert.Contains("Queue cannot exceed 1000 tracks", exception.Message);
    }

    [Fact]
    public void SetTracks_Should_Accept_Exactly_1000_Tracks()
    {
        // Arrange
        var queue = new PlaybackQueue { UserId = "user123" };
        var maxTracks = Enumerable.Range(1, 1000).Select(i => $"track{i}").ToList();

        // Act
        queue.SetTracks(maxTracks);

        // Assert
        Assert.Equal(1000, queue.GetTrackCount());
    }

    [Fact]
    public void SetTracks_Should_Handle_Empty_List()
    {
        // Arrange
        var queue = new PlaybackQueue { UserId = "user123" };
        var emptyList = new List<string>();

        // Act
        queue.SetTracks(emptyList);

        // Assert
        Assert.NotNull(queue.QueueState.TrackIds);
        Assert.Empty(queue.QueueState.TrackIds);
        Assert.Null(queue.CurrentTrackId);
        Assert.Equal(0, queue.CurrentIndex);
    }

    #endregion

    #region RepeatMode Enum Tests

    [Theory]
    [InlineData(RepeatMode.None, 0)]
    [InlineData(RepeatMode.One, 1)]
    [InlineData(RepeatMode.All, 2)]
    public void RepeatMode_Should_Have_Correct_Values(RepeatMode mode, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)mode);
    }

    #endregion

    #region QueueState Class Tests

    [Fact]
    public void QueueState_Should_Initialize_With_Empty_Lists()
    {
        // Arrange & Act
        var state = new QueueState();

        // Assert
        Assert.NotNull(state.TrackIds);
        Assert.NotNull(state.OriginalTrackIds);
        Assert.Empty(state.TrackIds);
        Assert.Empty(state.OriginalTrackIds);
        Assert.Null(state.ShuffledTrackIds);
        Assert.Null(state.Metadata);
    }

    [Fact]
    public void QueueState_Should_Serialize_And_Deserialize_Correctly()
    {
        // Arrange
        var state = new QueueState
        {
            TrackIds = new List<string> { "t1", "t2", "t3" },
            OriginalTrackIds = new List<string> { "t1", "t2", "t3" },
            ShuffledTrackIds = new List<string> { "t3", "t1", "t2" },
            Metadata = new Dictionary<string, object>
            {
                { "source", "playlist" },
                { "playlistId", "playlist123" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(state);
        var deserialized = JsonSerializer.Deserialize<QueueState>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.TrackIds?.Count);
        Assert.Equal(3, deserialized.OriginalTrackIds?.Count);
        Assert.Equal(3, deserialized.ShuffledTrackIds?.Count);
        Assert.NotNull(deserialized.Metadata);
        Assert.Equal(2, deserialized.Metadata.Count);
    }

    #endregion
}