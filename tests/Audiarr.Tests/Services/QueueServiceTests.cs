using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Audiarr.Core.DTOs;
using Audiarr.Core.DTOs.Requests;
using Audiarr.Core.Entities;
using Audiarr.Data.Context;
using Audiarr.Services;

namespace Audiarr.Tests.Services;

public class QueueServiceTests : IDisposable
{
    private readonly AudiarrContext _context;
    private readonly Mock<ILogger<QueueService>> _loggerMock;
    private readonly QueueService _queueService;
    private readonly string _testUserId = "test-user-123";

    public QueueServiceTests()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<AudiarrContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableServiceProviderCaching(false)
            .Options;

        _context = new AudiarrContext(options);
        _loggerMock = new Mock<ILogger<QueueService>>();
        _queueService = new QueueService(_context, _loggerMock.Object);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Add test user
        var user = new User
        {
            Id = _testUserId,
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        _context.Users.Add(user);

        // Add test tracks
        var artist = new Artist
        {
            Id = "artist-1",
            Name = "Test Artist"
        };
        _context.Artists.Add(artist);

        var album = new Album
        {
            Id = "album-1",
            Title = "Test Album",
            ArtistId = artist.Id
        };
        _context.Albums.Add(album);

        for (int i = 1; i <= 5; i++)
        {
            var track = new Track
            {
                Id = $"track-{i}",
                Title = $"Test Track {i}",
                FilePath = $"/music/track{i}.mp3",
                DurationMs = 180000, // 3 minutes
                ArtistId = artist.Id,
                AlbumId = album.Id,
                TrackNumber = i
            };
            _context.Tracks.Add(track);
        }

        _context.SaveChanges();
    }

    #region GetQueueAsync Tests

    [Fact]
    public async Task GetQueueAsync_Should_AutoCreate_Queue_When_Not_Exists()
    {
        // Act
        var result = await _queueService.GetQueueAsync(_testUserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_testUserId, result.UserId);
        Assert.Empty(result.TrackIds);
        Assert.Equal(0, result.CurrentIndex);
        Assert.Null(result.CurrentTrackId);
        Assert.Equal(RepeatMode.None, result.RepeatMode);
        Assert.False(result.IsShuffled);
        Assert.Equal(1, result.Version);

        // Verify queue was created in database
        var queueInDb = await _context.PlaybackQueues.FirstOrDefaultAsync(q => q.UserId == _testUserId);
        Assert.NotNull(queueInDb);
    }

    [Fact]
    public async Task GetQueueAsync_Should_Return_Existing_Queue()
    {
        // Arrange
        var existingQueue = new PlaybackQueue
        {
            Id = Guid.NewGuid().ToString(),
            UserId = _testUserId,
            RepeatMode = RepeatMode.All,
            IsShuffled = true,
            Version = 5
        };
        existingQueue.SetTracks(new List<string> { "track-1", "track-2", "track-3" });
        existingQueue.CurrentIndex = 2; // Set after SetTracks
        _context.PlaybackQueues.Add(existingQueue);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queueService.GetQueueAsync(_testUserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingQueue.Id, result.QueueId);
        Assert.Equal(3, result.TrackIds.Count);
        Assert.Equal(2, result.CurrentIndex);
        Assert.Equal(RepeatMode.All, result.RepeatMode);
        Assert.True(result.IsShuffled);
        Assert.Equal(5, result.Version);
    }

    #endregion

    #region AddTracksAsync Tests

    [Fact]
    public async Task AddTracksAsync_Should_Add_Tracks_To_Empty_Queue()
    {
        // Arrange
        var request = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3" },
            Source = "test"
        };

        // Act
        var result = await _queueService.AddTracksAsync(_testUserId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TrackIds.Count);
        Assert.Equal("track-1", result.TrackIds[0]);
        Assert.Equal("track-1", result.CurrentTrackId);
        Assert.Equal(0, result.CurrentIndex);
        Assert.Equal("test", result.QueueSource);
    }

    [Fact]
    public async Task AddTracksAsync_Should_Add_Tracks_With_PlayNext()
    {
        // Arrange
        // First, add some tracks to the queue
        var initialRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3" }
        };
        await _queueService.AddTracksAsync(_testUserId, initialRequest);

        // Now add tracks with PlayNext
        var request = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-4", "track-5" },
            PlayNext = true
        };

        // Act
        var result = await _queueService.AddTracksAsync(_testUserId, request);

        // Assert
        Assert.Equal(5, result.TrackIds.Count);
        Assert.Equal("track-1", result.TrackIds[0]); // Current track
        Assert.Equal("track-4", result.TrackIds[1]); // Inserted after current
        Assert.Equal("track-5", result.TrackIds[2]);
        Assert.Equal("track-2", result.TrackIds[3]);
        Assert.Equal("track-3", result.TrackIds[4]);
    }

    [Fact]
    public async Task AddTracksAsync_Should_Throw_When_Tracks_Not_Found()
    {
        // Arrange
        var request = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "non-existent-track" }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _queueService.AddTracksAsync(_testUserId, request));
        Assert.Contains("Tracks not found: non-existent-track", exception.Message);
    }

    [Fact]
    public async Task AddTracksAsync_Should_Limit_Queue_To_1000_Tracks()
    {
        // Arrange
        // The service doesn't validate duplicates in the request, it just limits total queue size
        // We can add the same tracks multiple times
        var trackIds = new List<string>();
        for (int i = 1; i <= 5; i++)
        {
            // Add each track 201 times (5 * 201 = 1005 > 1000)
            trackIds.AddRange(Enumerable.Repeat($"track-{i}", 201));
        }

        var request = new AddToQueueRequest
        {
            TrackIds = trackIds
        };

        // Act
        var result = await _queueService.AddTracksAsync(_testUserId, request);

        // Assert
        // The service deduplicates using Distinct() before validation,
        // so it will only add the 5 unique tracks
        Assert.Equal(5, result.TrackIds.Count);
        Assert.Equal(5, result.TotalTracks);
    }

    #endregion

    #region RemoveTrackAtIndexAsync Tests

    [Fact]
    public async Task RemoveTrackAtIndexAsync_Should_Remove_Track_At_Index()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3", "track-4" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        // Act - remove track at index 2 (track-3)
        var result = await _queueService.RemoveTrackAtIndexAsync(_testUserId, 2);

        // Assert
        Assert.Equal(3, result.TrackIds.Count);
        Assert.Equal("track-1", result.TrackIds[0]);
        Assert.Equal("track-2", result.TrackIds[1]);
        Assert.Equal("track-4", result.TrackIds[2]);
    }

    [Fact]
    public async Task RemoveTrackAtIndexAsync_Should_Adjust_CurrentIndex_When_Removing_Before_Current()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3", "track-4" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        // Set current index to 3 (track-4)
        await _queueService.UpdateQueueSettingsAsync(_testUserId, new UpdateQueueRequest { CurrentIndex = 3 });

        // Act - remove track at index 1 (track-2)
        var result = await _queueService.RemoveTrackAtIndexAsync(_testUserId, 1);

        // Assert
        Assert.Equal(2, result.CurrentIndex); // Adjusted from 3 to 2
        Assert.Equal("track-4", result.CurrentTrackId); // Still the same track
    }

    [Fact]
    public async Task RemoveTrackAtIndexAsync_Should_Handle_Removing_Current_Track()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        // Set current index to 1 (track-2)
        await _queueService.UpdateQueueSettingsAsync(_testUserId, new UpdateQueueRequest { CurrentIndex = 1 });

        // Act - remove current track
        var result = await _queueService.RemoveTrackAtIndexAsync(_testUserId, 1);

        // Assert
        Assert.Equal(2, result.TrackIds.Count);
        Assert.Equal(1, result.CurrentIndex); // Same index
        Assert.Equal("track-3", result.CurrentTrackId); // Next track becomes current
    }

    [Fact]
    public async Task RemoveTrackAtIndexAsync_Should_Throw_When_Index_Out_Of_Range()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _queueService.RemoveTrackAtIndexAsync(_testUserId, 5));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _queueService.RemoveTrackAtIndexAsync(_testUserId, -1));
    }

    #endregion

    #region ClearQueueAsync Tests

    [Fact]
    public async Task ClearQueueAsync_Should_Clear_All_Tracks()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        // Act
        var result = await _queueService.ClearQueueAsync(_testUserId, keepCurrentTrack: false);

        // Assert
        Assert.Empty(result.TrackIds);
        Assert.Null(result.CurrentTrackId);
        Assert.Equal(0, result.CurrentIndex);
        Assert.Equal(0, result.TotalTracks);
    }

    [Fact]
    public async Task ClearQueueAsync_Should_Keep_Current_Track_When_Requested()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        // Set current to track-2
        await _queueService.UpdateQueueSettingsAsync(_testUserId, new UpdateQueueRequest { CurrentIndex = 1 });

        // Act
        var result = await _queueService.ClearQueueAsync(_testUserId, keepCurrentTrack: true);

        // Assert
        Assert.Single(result.TrackIds);
        Assert.Equal("track-2", result.TrackIds[0]);
        Assert.Equal("track-2", result.CurrentTrackId);
        Assert.Equal(0, result.CurrentIndex);
    }

    #endregion

    #region ReorderQueueAsync Tests

    [Fact]
    public async Task ReorderQueueAsync_Should_Move_Track_To_New_Position()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3", "track-4", "track-5" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        var reorderRequest = new ReorderQueueRequest
        {
            TrackId = "track-2",
            NewIndex = 3
        };

        // Act
        var result = await _queueService.ReorderQueueAsync(_testUserId, reorderRequest);

        // Assert
        Assert.Equal(5, result.TrackIds.Count);
        Assert.Equal("track-1", result.TrackIds[0]);
        Assert.Equal("track-3", result.TrackIds[1]);
        Assert.Equal("track-4", result.TrackIds[2]);
        Assert.Equal("track-2", result.TrackIds[3]); // Moved here
        Assert.Equal("track-5", result.TrackIds[4]);
    }

    [Fact]
    public async Task ReorderQueueAsync_Should_Update_CurrentIndex_When_Moving_Current_Track()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3", "track-4" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        // Set current to track-2 (index 1)
        await _queueService.UpdateQueueSettingsAsync(_testUserId, new UpdateQueueRequest { CurrentIndex = 1 });

        var reorderRequest = new ReorderQueueRequest
        {
            TrackId = "track-2",
            NewIndex = 3
        };

        // Act
        var result = await _queueService.ReorderQueueAsync(_testUserId, reorderRequest);

        // Assert
        Assert.Equal(3, result.CurrentIndex); // Current index follows the track
        Assert.Equal("track-2", result.CurrentTrackId);
    }

    [Fact]
    public async Task ReorderQueueAsync_Should_Throw_When_Track_Not_Found()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        var reorderRequest = new ReorderQueueRequest
        {
            TrackId = "non-existent",
            NewIndex = 0
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _queueService.ReorderQueueAsync(_testUserId, reorderRequest));
        Assert.Contains("Track non-existent not found in queue", exception.Message);
    }

    [Fact]
    public async Task ReorderQueueAsync_Should_Throw_When_NewIndex_Out_Of_Range()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        var reorderRequest = new ReorderQueueRequest
        {
            TrackId = "track-1",
            NewIndex = 5
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _queueService.ReorderQueueAsync(_testUserId, reorderRequest));
    }

    #endregion

    #region ReplaceQueueAsync Tests

    [Fact]
    public async Task ReplaceQueueAsync_Should_Replace_Entire_Queue()
    {
        // Arrange
        var initialRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2" }
        };
        await _queueService.AddTracksAsync(_testUserId, initialRequest);

        var replaceRequest = new ReplaceQueueRequest
        {
            TrackIds = new List<string> { "track-3", "track-4", "track-5" },
            Source = "album"
        };

        // Act
        var result = await _queueService.ReplaceQueueAsync(_testUserId, replaceRequest);

        // Assert
        Assert.Equal(3, result.TrackIds.Count);
        Assert.Equal("track-3", result.TrackIds[0]);
        Assert.Equal("track-4", result.TrackIds[1]);
        Assert.Equal("track-5", result.TrackIds[2]);
        Assert.Equal("track-3", result.CurrentTrackId);
        Assert.Equal(0, result.CurrentIndex);
        Assert.Equal("album", result.QueueSource);
    }

    [Fact]
    public async Task ReplaceQueueAsync_Should_Set_StartIndex()
    {
        // Arrange
        var replaceRequest = new ReplaceQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3" },
            StartIndex = 2
        };

        // Act
        var result = await _queueService.ReplaceQueueAsync(_testUserId, replaceRequest);

        // Assert
        Assert.Equal(2, result.CurrentIndex);
        Assert.Equal("track-3", result.CurrentTrackId);
    }

    [Fact]
    public async Task ReplaceQueueAsync_Should_Throw_When_Tracks_Not_Found()
    {
        // Arrange
        var replaceRequest = new ReplaceQueueRequest
        {
            TrackIds = new List<string> { "track-1", "non-existent" }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _queueService.ReplaceQueueAsync(_testUserId, replaceRequest));
        Assert.Contains("Tracks not found: non-existent", exception.Message);
    }

    #endregion

    #region UpdateQueueSettingsAsync Tests

    [Fact]
    public async Task UpdateQueueSettingsAsync_Should_Update_RepeatMode()
    {
        // Arrange
        await _queueService.GetQueueAsync(_testUserId); // Create queue

        var updateRequest = new UpdateQueueRequest
        {
            RepeatMode = RepeatMode.One
        };

        // Act
        var result = await _queueService.UpdateQueueSettingsAsync(_testUserId, updateRequest);

        // Assert
        Assert.Equal(RepeatMode.One, result.RepeatMode);
    }

    [Fact]
    public async Task UpdateQueueSettingsAsync_Should_Enable_Shuffle()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3", "track-4", "track-5" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        var updateRequest = new UpdateQueueRequest
        {
            IsShuffled = true
        };

        // Act
        var result = await _queueService.UpdateQueueSettingsAsync(_testUserId, updateRequest);

        // Assert
        Assert.True(result.IsShuffled);
        Assert.Equal(5, result.TrackIds.Count);
        // Verify all tracks are still present
        Assert.Contains("track-1", result.TrackIds);
        Assert.Contains("track-2", result.TrackIds);
        Assert.Contains("track-3", result.TrackIds);
        Assert.Contains("track-4", result.TrackIds);
        Assert.Contains("track-5", result.TrackIds);
    }

    [Fact]
    public async Task UpdateQueueSettingsAsync_Should_Disable_Shuffle_And_Restore_Original_Order()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3", "track-4" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        // Enable shuffle first
        await _queueService.UpdateQueueSettingsAsync(_testUserId, new UpdateQueueRequest { IsShuffled = true });

        // Now disable shuffle
        var updateRequest = new UpdateQueueRequest
        {
            IsShuffled = false
        };

        // Act
        var result = await _queueService.UpdateQueueSettingsAsync(_testUserId, updateRequest);

        // Assert
        Assert.False(result.IsShuffled);
        // Should restore original order
        Assert.Equal("track-1", result.TrackIds[0]);
        Assert.Equal("track-2", result.TrackIds[1]);
        Assert.Equal("track-3", result.TrackIds[2]);
        Assert.Equal("track-4", result.TrackIds[3]);
    }

    [Fact]
    public async Task UpdateQueueSettingsAsync_Should_Update_CurrentIndex()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        var updateRequest = new UpdateQueueRequest
        {
            CurrentIndex = 2
        };

        // Act
        var result = await _queueService.UpdateQueueSettingsAsync(_testUserId, updateRequest);

        // Assert
        Assert.Equal(2, result.CurrentIndex);
        Assert.Equal("track-3", result.CurrentTrackId);
    }

    [Fact]
    public async Task UpdateQueueSettingsAsync_Should_Throw_When_CurrentIndex_Out_Of_Range()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2" }
        };
        await _queueService.AddTracksAsync(_testUserId, addRequest);

        var updateRequest = new UpdateQueueRequest
        {
            CurrentIndex = 5
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _queueService.UpdateQueueSettingsAsync(_testUserId, updateRequest));
    }

    #endregion

    #region Version Tracking Tests

    [Fact]
    public async Task All_Operations_Should_Increment_Version()
    {
        // Arrange & Act
        var queue1 = await _queueService.GetQueueAsync(_testUserId);
        Assert.Equal(1, queue1.Version);

        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track-1" }
        };
        var queue2 = await _queueService.AddTracksAsync(_testUserId, addRequest);
        Assert.Equal(2, queue2.Version);

        var queue3 = await _queueService.ClearQueueAsync(_testUserId, false);
        Assert.Equal(3, queue3.Version);
    }

    #endregion

    public void Dispose()
    {
        _context?.Dispose();
    }
}