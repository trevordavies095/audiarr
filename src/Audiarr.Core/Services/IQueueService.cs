using Audiarr.Core.DTOs;
using Audiarr.Core.DTOs.Requests;

namespace Audiarr.Core.Services;

public interface IQueueService
{
    Task<QueueStateDto> GetQueueAsync(string userId);
    Task<QueueStateDto> AddTracksAsync(string userId, AddToQueueRequest request);
    Task<QueueStateDto> RemoveTrackAtIndexAsync(string userId, int index);
    Task<QueueStateDto> ClearQueueAsync(string userId, bool keepCurrentTrack = false);
    Task<QueueStateDto> ReorderQueueAsync(string userId, ReorderQueueRequest request);
    Task<QueueStateDto> ReplaceQueueAsync(string userId, ReplaceQueueRequest request);
    Task<QueueStateDto> UpdateQueueSettingsAsync(string userId, UpdateQueueRequest request);
    
    // Playback control methods
    Task<QueueStateDto> NextTrackAsync(string userId);
    Task<QueueStateDto> PreviousTrackAsync(string userId);
    Task<QueueStateDto> JumpToPositionAsync(string userId, int index);
}