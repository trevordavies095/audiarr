using System.Text.Json;
using Audiarr.Core.DTOs;
using Audiarr.Core.Entities;
using Xunit;

namespace Audiarr.Tests.DTOs;

public class QueueDtoTests
{
    #region QueueStateDto Tests

    [Fact]
    public void QueueStateDto_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var dto = new QueueStateDto();

        // Assert
        Assert.Equal(string.Empty, dto.QueueId);
        Assert.Equal(string.Empty, dto.UserId);
        Assert.NotNull(dto.TrackIds);
        Assert.Empty(dto.TrackIds);
        Assert.Null(dto.CurrentTrackId);
        Assert.Equal(0, dto.CurrentIndex);
        Assert.Equal(RepeatMode.None, dto.RepeatMode);
        Assert.False(dto.IsShuffled);
        Assert.Equal(0, dto.TotalTracks);
        Assert.Null(dto.QueueSource);
        Assert.Equal(0, dto.Version);
    }

    [Fact]
    public void QueueStateDto_CanSetAllProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var trackIds = new List<string> { "track1", "track2", "track3" };

        // Act
        var dto = new QueueStateDto
        {
            QueueId = "queue123",
            UserId = "user456",
            TrackIds = trackIds,
            CurrentTrackId = "track2",
            CurrentIndex = 1,
            RepeatMode = RepeatMode.All,
            IsShuffled = true,
            TotalTracks = 3,
            QueueSource = "playlist",
            LastActivity = now,
            Version = 2
        };

        // Assert
        Assert.Equal("queue123", dto.QueueId);
        Assert.Equal("user456", dto.UserId);
        Assert.Equal(3, dto.TrackIds.Count);
        Assert.Equal("track2", dto.CurrentTrackId);
        Assert.Equal(1, dto.CurrentIndex);
        Assert.Equal(RepeatMode.All, dto.RepeatMode);
        Assert.True(dto.IsShuffled);
        Assert.Equal(3, dto.TotalTracks);
        Assert.Equal("playlist", dto.QueueSource);
        Assert.Equal(now, dto.LastActivity);
        Assert.Equal(2, dto.Version);
    }

    [Fact]
    public void QueueStateDto_SerializesCorrectly()
    {
        // Arrange
        var dto = new QueueStateDto
        {
            QueueId = "queue123",
            UserId = "user456",
            TrackIds = new List<string> { "track1", "track2" },
            CurrentTrackId = "track1",
            CurrentIndex = 0,
            RepeatMode = RepeatMode.One,
            IsShuffled = false,
            TotalTracks = 2,
            QueueSource = "album",
            LastActivity = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Version = 1
        };

        // Act
        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<QueueStateDto>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(dto.QueueId, deserialized.QueueId);
        Assert.Equal(dto.UserId, deserialized.UserId);
        Assert.Equal(dto.TrackIds.Count, deserialized.TrackIds.Count);
        Assert.Equal(dto.CurrentTrackId, deserialized.CurrentTrackId);
        Assert.Equal(dto.CurrentIndex, deserialized.CurrentIndex);
        Assert.Equal(dto.RepeatMode, deserialized.RepeatMode);
        Assert.Equal(dto.IsShuffled, deserialized.IsShuffled);
        Assert.Equal(dto.TotalTracks, deserialized.TotalTracks);
        Assert.Equal(dto.QueueSource, deserialized.QueueSource);
        Assert.Equal(dto.LastActivity, deserialized.LastActivity);
        Assert.Equal(dto.Version, deserialized.Version);
    }

    [Theory]
    [InlineData(RepeatMode.None)]
    [InlineData(RepeatMode.One)]
    [InlineData(RepeatMode.All)]
    public void QueueStateDto_RepeatModeEnum_SerializesCorrectly(RepeatMode mode)
    {
        // Arrange
        var dto = new QueueStateDto { RepeatMode = mode };

        // Act
        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<QueueStateDto>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(mode, deserialized.RepeatMode);
    }

    [Fact]
    public void QueueStateDto_EmptyTrackIds_SerializesAsEmptyArray()
    {
        // Arrange
        var dto = new QueueStateDto
        {
            TrackIds = new List<string>()
        };

        // Act
        var json = JsonSerializer.Serialize(dto);

        // Assert
        Assert.Contains("\"TrackIds\":[]", json.Replace(" ", ""));
    }

    [Fact]
    public void QueueStateDto_NullCurrentTrackId_SerializesAsNull()
    {
        // Arrange
        var dto = new QueueStateDto
        {
            CurrentTrackId = null
        };

        // Act
        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<QueueStateDto>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.CurrentTrackId);
    }

    #endregion

    #region QueueItemDto Tests

    [Fact]
    public void QueueItemDto_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var dto = new QueueItemDto();

        // Assert
        Assert.Equal(0, dto.Index);
        Assert.Equal(string.Empty, dto.TrackId);
        Assert.Null(dto.Track);
        Assert.Equal(default(DateTime), dto.AddedAt);
        Assert.Null(dto.Source);
    }

    [Fact]
    public void QueueItemDto_CanSetAllProperties()
    {
        // Arrange
        var track = new TrackDto
        {
            Id = "track123",
            Title = "Test Song",
            ArtistName = "Test Artist",
            AlbumTitle = "Test Album",
            DurationMs = 180000
        };
        var addedAt = DateTime.UtcNow;

        // Act
        var dto = new QueueItemDto
        {
            Index = 5,
            TrackId = "track123",
            Track = track,
            AddedAt = addedAt,
            Source = "search"
        };

        // Assert
        Assert.Equal(5, dto.Index);
        Assert.Equal("track123", dto.TrackId);
        Assert.NotNull(dto.Track);
        Assert.Equal("Test Song", dto.Track.Title);
        Assert.Equal(addedAt, dto.AddedAt);
        Assert.Equal("search", dto.Source);
    }

    [Fact]
    public void QueueItemDto_SerializesCorrectly()
    {
        // Arrange
        var dto = new QueueItemDto
        {
            Index = 10,
            TrackId = "track456",
            Track = new TrackDto
            {
                Id = "track456",
                Title = "Another Song",
                ArtistName = "Another Artist",
                AlbumTitle = "Another Album",
                DurationMs = 240000
            },
            AddedAt = new DateTime(2025, 1, 1, 15, 30, 0, DateTimeKind.Utc),
            Source = "playlist"
        };

        // Act
        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<QueueItemDto>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(dto.Index, deserialized.Index);
        Assert.Equal(dto.TrackId, deserialized.TrackId);
        Assert.NotNull(deserialized.Track);
        Assert.Equal(dto.Track.Id, deserialized.Track.Id);
        Assert.Equal(dto.Track.Title, deserialized.Track.Title);
        Assert.Equal(dto.AddedAt, deserialized.AddedAt);
        Assert.Equal(dto.Source, deserialized.Source);
    }

    [Fact]
    public void QueueItemDto_WithNullSource_SerializesCorrectly()
    {
        // Arrange
        var dto = new QueueItemDto
        {
            Index = 0,
            TrackId = "track789",
            Track = new TrackDto { Id = "track789", Title = "Song" },
            AddedAt = DateTime.UtcNow,
            Source = null
        };

        // Act
        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<QueueItemDto>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.Source);
    }

    [Fact]
    public void QueueItemDto_ListSerialization_WorksCorrectly()
    {
        // Arrange
        var items = new List<QueueItemDto>
        {
            new QueueItemDto
            {
                Index = 0,
                TrackId = "track1",
                Track = new TrackDto { Id = "track1", Title = "Song 1" },
                AddedAt = DateTime.UtcNow,
                Source = "album"
            },
            new QueueItemDto
            {
                Index = 1,
                TrackId = "track2",
                Track = new TrackDto { Id = "track2", Title = "Song 2" },
                AddedAt = DateTime.UtcNow,
                Source = "album"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(items);
        var deserialized = JsonSerializer.Deserialize<List<QueueItemDto>>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);
        Assert.Equal("track1", deserialized[0].TrackId);
        Assert.Equal("track2", deserialized[1].TrackId);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void QueueStateDto_CanRepresentEmptyQueue()
    {
        // Arrange & Act
        var emptyQueue = new QueueStateDto
        {
            QueueId = "empty-queue",
            UserId = "user123",
            TrackIds = new List<string>(),
            CurrentTrackId = null,
            CurrentIndex = 0,
            TotalTracks = 0,
            RepeatMode = RepeatMode.None,
            IsShuffled = false
        };

        // Assert
        Assert.Empty(emptyQueue.TrackIds);
        Assert.Null(emptyQueue.CurrentTrackId);
        Assert.Equal(0, emptyQueue.TotalTracks);
    }

    [Fact]
    public void QueueStateDto_CanRepresentShuffledQueue()
    {
        // Arrange & Act
        var shuffledQueue = new QueueStateDto
        {
            QueueId = "shuffled-queue",
            UserId = "user123",
            TrackIds = new List<string> { "track3", "track1", "track5", "track2", "track4" },
            CurrentTrackId = "track3",
            CurrentIndex = 0,
            TotalTracks = 5,
            RepeatMode = RepeatMode.All,
            IsShuffled = true,
            QueueSource = "library"
        };

        // Assert
        Assert.True(shuffledQueue.IsShuffled);
        Assert.Equal(5, shuffledQueue.TotalTracks);
        Assert.Equal(RepeatMode.All, shuffledQueue.RepeatMode);
    }

    [Fact]
    public void QueueItemDto_CanRepresentQueueWithFullTrackInfo()
    {
        // Arrange
        var fullTrack = new TrackDto
        {
            Id = "track-full",
            Title = "Complete Track",
            ArtistId = "artist123",
            ArtistName = "Full Artist",
            AlbumId = "album456",
            AlbumTitle = "Full Album",
            TrackNumber = 5,
            DiscNumber = 1,
            DurationMs = 210000,
            Genre = "Rock",
            Year = 2024,
            FileSize = 8500000,
            Bitrate = 320,
            Codec = "MP3",
            FilePath = "/music/track.mp3"
        };

        // Act
        var queueItem = new QueueItemDto
        {
            Index = 0,
            TrackId = fullTrack.Id,
            Track = fullTrack,
            AddedAt = DateTime.UtcNow,
            Source = "album"
        };

        // Assert
        Assert.NotNull(queueItem.Track);
        Assert.Equal("Complete Track", queueItem.Track.Title);
        Assert.Equal(210000, queueItem.Track.DurationMs);
        Assert.Equal("Rock", queueItem.Track.Genre);
    }

    #endregion
}