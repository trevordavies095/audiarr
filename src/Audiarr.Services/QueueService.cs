using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Audiarr.Core.DTOs;
using Audiarr.Core.DTOs.Requests;
using Audiarr.Core.Entities;
using Audiarr.Core.Services;
using Audiarr.Data.Context;

namespace Audiarr.Services;

public class QueueService : IQueueService
{
    private readonly AudiarrContext _context;
    private readonly ILogger<QueueService> _logger;

    public QueueService(AudiarrContext context, ILogger<QueueService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<QueueStateDto> GetQueueAsync(string userId)
    {
        var queue = await GetOrCreateQueueAsync(userId);
        return MapToDto(queue);
    }

    public async Task<QueueStateDto> AddTracksAsync(string userId, AddToQueueRequest request)
    {
        var queue = await GetOrCreateQueueAsync(userId);

        // Validate tracks exist
        var trackIds = request.TrackIds.Distinct().ToList();
        var existingTracks = await _context.Tracks
            .Where(t => trackIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync();

        if (existingTracks.Count != trackIds.Count)
        {
            var missingTracks = trackIds.Except(existingTracks).ToList();
            throw new ArgumentException($"Tracks not found: {string.Join(", ", missingTracks)}");
        }

        var queueState = queue.QueueState;

        // Ensure TrackIds is initialized
        queueState.TrackIds ??= new List<string>();

        if (request.PlayNext && queue.CurrentIndex >= 0 && queue.CurrentIndex < queueState.TrackIds.Count)
        {
            // Insert after current track
            var insertIndex = queue.CurrentIndex + 1;
            queueState.TrackIds.InsertRange(insertIndex, trackIds);
        }
        else
        {
            // Append to end
            queueState.TrackIds.AddRange(trackIds);
        }

        // Ensure queue doesn't exceed 1000 tracks
        if (queueState.TrackIds.Count > 1000)
        {
            queueState.TrackIds = queueState.TrackIds.Take(1000).ToList();
        }

        // Set metadata if provided
        if (!string.IsNullOrEmpty(request.Source))
        {
            queueState.Metadata ??= new Dictionary<string, object>();
            queueState.Metadata["source"] = request.Source;
        }

        // Update queue
        queue.QueueState = queueState;

        // Set current track if queue was empty
        if (string.IsNullOrEmpty(queue.CurrentTrackId) && queueState.TrackIds.Any())
        {
            queue.CurrentTrackId = queueState.TrackIds.First();
            queue.CurrentIndex = 0;
        }

        queue.UpdateActivity();
        queue.Version++;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Added {Count} tracks to queue for user {UserId}", trackIds.Count, userId);

        return MapToDto(queue);
    }

    public async Task<QueueStateDto> RemoveTrackAtIndexAsync(string userId, int index)
    {
        var queue = await GetOrCreateQueueAsync(userId);
        var queueState = queue.QueueState;

        // Ensure TrackIds is initialized
        queueState.TrackIds ??= new List<string>();

        if (index < 0 || index >= queueState.TrackIds.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range");
        }

        var removedTrackId = queueState.TrackIds[index];
        queueState.TrackIds.RemoveAt(index);

        // Adjust current index if necessary
        if (queue.CurrentIndex > index)
        {
            queue.CurrentIndex--;
        }
        else if (queue.CurrentIndex == index)
        {
            // Current track was removed
            if (queueState.TrackIds.Any())
            {
                // Keep same index if possible, otherwise move to last track
                if (queue.CurrentIndex >= queueState.TrackIds.Count)
                {
                    queue.CurrentIndex = queueState.TrackIds.Count - 1;
                }
                queue.CurrentTrackId = queueState.TrackIds[queue.CurrentIndex];
            }
            else
            {
                // Queue is now empty
                queue.CurrentIndex = 0;
                queue.CurrentTrackId = null;
            }
        }

        queue.QueueState = queueState;
        queue.UpdateActivity();
        queue.Version++;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Removed track at index {Index} from queue for user {UserId}", index, userId);

        return MapToDto(queue);
    }

    public async Task<QueueStateDto> ClearQueueAsync(string userId, bool keepCurrentTrack = false)
    {
        var queue = await GetOrCreateQueueAsync(userId);

        if (keepCurrentTrack && !string.IsNullOrEmpty(queue.CurrentTrackId))
        {
            // Keep only the current track
            var queueState = queue.QueueState;
            queueState.TrackIds = new List<string> { queue.CurrentTrackId };
            queue.QueueState = queueState;
            queue.CurrentIndex = 0;
        }
        else
        {
            // Clear everything
            queue.ClearQueue();
        }

        queue.UpdateActivity();
        queue.Version++;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Cleared queue for user {UserId} (keepCurrent: {KeepCurrent})", userId, keepCurrentTrack);

        return MapToDto(queue);
    }

    public async Task<QueueStateDto> ReorderQueueAsync(string userId, ReorderQueueRequest request)
    {
        var queue = await GetOrCreateQueueAsync(userId);
        var queueState = queue.QueueState;

        // Ensure TrackIds is initialized
        queueState.TrackIds ??= new List<string>();

        // Find current position of the track
        var currentIndex = queueState.TrackIds.IndexOf(request.TrackId);
        if (currentIndex == -1)
        {
            throw new ArgumentException($"Track {request.TrackId} not found in queue");
        }

        if (request.NewIndex < 0 || request.NewIndex >= queueState.TrackIds.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(request.NewIndex), "New index is out of range");
        }

        // Remove from current position
        queueState.TrackIds.RemoveAt(currentIndex);

        // Insert at new position
        queueState.TrackIds.Insert(request.NewIndex, request.TrackId);

        // Adjust current index if necessary
        if (queue.CurrentTrackId == request.TrackId)
        {
            // The current track was moved
            queue.CurrentIndex = request.NewIndex;
        }
        else if (currentIndex < queue.CurrentIndex && request.NewIndex >= queue.CurrentIndex)
        {
            // Track moved from before to after current
            queue.CurrentIndex--;
        }
        else if (currentIndex > queue.CurrentIndex && request.NewIndex <= queue.CurrentIndex)
        {
            // Track moved from after to before current
            queue.CurrentIndex++;
        }

        queue.QueueState = queueState;
        queue.UpdateActivity();
        queue.Version++;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Reordered track {TrackId} from {OldIndex} to {NewIndex} in queue for user {UserId}",
            request.TrackId, currentIndex, request.NewIndex, userId);

        return MapToDto(queue);
    }

    public async Task<QueueStateDto> ReplaceQueueAsync(string userId, ReplaceQueueRequest request)
    {
        var queue = await GetOrCreateQueueAsync(userId);

        // Validate tracks exist
        var trackIds = request.TrackIds.Distinct().ToList();
        var existingTracks = await _context.Tracks
            .Where(t => trackIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync();

        if (existingTracks.Count != trackIds.Count)
        {
            var missingTracks = trackIds.Except(existingTracks).ToList();
            throw new ArgumentException($"Tracks not found: {string.Join(", ", missingTracks)}");
        }

        // Replace queue with new tracks
        queue.SetTracks(trackIds, shuffle: false);

        // Set start index if specified
        if (request.StartIndex > 0 && request.StartIndex < trackIds.Count)
        {
            queue.CurrentIndex = request.StartIndex;
            queue.CurrentTrackId = trackIds[request.StartIndex];
        }

        // Set metadata if provided
        if (!string.IsNullOrEmpty(request.Source))
        {
            var queueState = queue.QueueState;
            queueState.Metadata ??= new Dictionary<string, object>();
            queueState.Metadata["source"] = request.Source;
            queue.QueueState = queueState;
        }

        queue.UpdateActivity();
        queue.Version++;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Replaced queue with {Count} tracks for user {UserId}", trackIds.Count, userId);

        return MapToDto(queue);
    }

    public async Task<QueueStateDto> UpdateQueueSettingsAsync(string userId, UpdateQueueRequest request)
    {
        var queue = await GetOrCreateQueueAsync(userId);

        if (request.RepeatMode.HasValue)
        {
            queue.RepeatMode = request.RepeatMode.Value;
        }

        if (request.IsShuffled.HasValue)
        {
            if (request.IsShuffled.Value != queue.IsShuffled)
            {
                if (request.IsShuffled.Value)
                {
                    // Enable shuffle
                    var queueState = queue.QueueState;
                    if (queueState.TrackIds?.Any() == true)
                    {
                        // Store original order
                        queueState.OriginalTrackIds = new List<string>(queueState.TrackIds);

                        // Create shuffled order (keeping current track in place)
                        var currentTrack = queue.CurrentTrackId;
                        var otherTracks = queueState.TrackIds.Where(t => t != currentTrack).ToList();
                        var shuffled = otherTracks.OrderBy(_ => Guid.NewGuid()).ToList();

                        if (!string.IsNullOrEmpty(currentTrack))
                        {
                            shuffled.Insert(queue.CurrentIndex, currentTrack);
                        }

                        queueState.ShuffledTrackIds = shuffled;
                        queueState.TrackIds = shuffled;
                        queue.QueueState = queueState;
                    }
                }
                else
                {
                    // Disable shuffle - restore original order
                    var queueState = queue.QueueState;
                    if (queueState.OriginalTrackIds?.Any() == true)
                    {
                        // Find current track in original order
                        var currentTrack = queue.CurrentTrackId;
                        queueState.TrackIds = new List<string>(queueState.OriginalTrackIds);

                        if (!string.IsNullOrEmpty(currentTrack))
                        {
                            queue.CurrentIndex = queueState.TrackIds.IndexOf(currentTrack);
                            if (queue.CurrentIndex == -1)
                            {
                                queue.CurrentIndex = 0;
                            }
                        }

                        queueState.ShuffledTrackIds = null;
                        queue.QueueState = queueState;
                    }
                }

                queue.IsShuffled = request.IsShuffled.Value;
            }
        }

        if (request.CurrentIndex.HasValue)
        {
            var queueState = queue.QueueState;
            queueState.TrackIds ??= new List<string>();
            if (request.CurrentIndex.Value >= 0 && request.CurrentIndex.Value < queueState.TrackIds.Count)
            {
                queue.CurrentIndex = request.CurrentIndex.Value;
                queue.CurrentTrackId = queueState.TrackIds[request.CurrentIndex.Value];
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(request.CurrentIndex), "Current index is out of range");
            }
        }

        queue.UpdateActivity();
        queue.Version++;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Updated queue settings for user {UserId}", userId);

        return MapToDto(queue);
    }

    private async Task<PlaybackQueue> GetOrCreateQueueAsync(string userId)
    {
        var queue = await _context.PlaybackQueues
            .FirstOrDefaultAsync(q => q.UserId == userId);

        if (queue == null)
        {
            // Auto-create queue for user
            queue = new PlaybackQueue
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId
            };

            _context.PlaybackQueues.Add(queue);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Auto-created queue for user {UserId}", userId);
        }

        return queue;
    }

    public async Task<QueueStateDto> NextTrackAsync(string userId)
    {
        var queue = await GetOrCreateQueueAsync(userId);
        var queueState = queue.QueueState;

        // Ensure TrackIds is initialized
        queueState.TrackIds ??= new List<string>();

        if (!queueState.TrackIds.Any())
        {
            throw new InvalidOperationException("Queue is empty");
        }

        var trackCount = queueState.TrackIds.Count;
        var currentIndex = queue.CurrentIndex;
        int nextIndex;

        // Handle based on repeat mode
        if (queue.RepeatMode == RepeatMode.One)
        {
            // Always stay on current track when repeat one is enabled
            nextIndex = currentIndex;
        }
        else
        {
            nextIndex = currentIndex + 1;
            
            // Handle edge cases at end of queue
            if (nextIndex >= trackCount)
            {
                switch (queue.RepeatMode)
                {
                    case RepeatMode.All:
                        // Loop back to first track
                        nextIndex = 0;
                        break;
                    case RepeatMode.None:
                    default:
                        // Stay on last track
                        nextIndex = trackCount - 1;
                        break;
                }
            }
        }

        queue.CurrentIndex = nextIndex;
        queue.CurrentTrackId = queueState.TrackIds[nextIndex];
        queue.UpdateActivity();
        queue.Version++;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Moved to next track (index {Index}) in queue for user {UserId}", nextIndex, userId);

        return MapToDto(queue);
    }

    public async Task<QueueStateDto> PreviousTrackAsync(string userId)
    {
        var queue = await GetOrCreateQueueAsync(userId);
        var queueState = queue.QueueState;

        // Ensure TrackIds is initialized
        queueState.TrackIds ??= new List<string>();

        if (!queueState.TrackIds.Any())
        {
            throw new InvalidOperationException("Queue is empty");
        }

        var trackCount = queueState.TrackIds.Count;
        var currentIndex = queue.CurrentIndex;
        int previousIndex;

        // Handle based on repeat mode
        if (queue.RepeatMode == RepeatMode.One)
        {
            // Always stay on current track when repeat one is enabled
            previousIndex = currentIndex;
        }
        else
        {
            previousIndex = currentIndex - 1;
            
            // Handle edge cases at start of queue
            if (previousIndex < 0)
            {
                switch (queue.RepeatMode)
                {
                    case RepeatMode.All:
                        // Loop to last track
                        previousIndex = trackCount - 1;
                        break;
                    case RepeatMode.None:
                    default:
                        // Stay on first track
                        previousIndex = 0;
                        break;
                }
            }
        }

        queue.CurrentIndex = previousIndex;
        queue.CurrentTrackId = queueState.TrackIds[previousIndex];
        queue.UpdateActivity();
        queue.Version++;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Moved to previous track (index {Index}) in queue for user {UserId}", previousIndex, userId);

        return MapToDto(queue);
    }

    public async Task<QueueStateDto> JumpToPositionAsync(string userId, int index)
    {
        var queue = await GetOrCreateQueueAsync(userId);
        var queueState = queue.QueueState;

        // Ensure TrackIds is initialized
        queueState.TrackIds ??= new List<string>();

        if (!queueState.TrackIds.Any())
        {
            throw new InvalidOperationException("Queue is empty");
        }

        if (index < 0 || index >= queueState.TrackIds.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range. Queue has {queueState.TrackIds.Count} tracks.");
        }

        queue.CurrentIndex = index;
        queue.CurrentTrackId = queueState.TrackIds[index];
        queue.UpdateActivity();
        queue.Version++;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Jumped to position {Index} in queue for user {UserId}", index, userId);

        return MapToDto(queue);
    }

    private QueueStateDto MapToDto(PlaybackQueue queue)
    {
        var queueState = queue.QueueState;
        var source = queueState.Metadata?.GetValueOrDefault("source")?.ToString();

        return new QueueStateDto
        {
            QueueId = queue.Id,
            UserId = queue.UserId,
            TrackIds = queueState.TrackIds ?? new List<string>(),
            CurrentTrackId = queue.CurrentTrackId,
            CurrentIndex = queue.CurrentIndex,
            RepeatMode = queue.RepeatMode,
            IsShuffled = queue.IsShuffled,
            TotalTracks = queueState.TrackIds?.Count ?? 0,
            QueueSource = source,
            LastActivity = queue.LastActivity,
            Version = queue.Version
        };
    }
}