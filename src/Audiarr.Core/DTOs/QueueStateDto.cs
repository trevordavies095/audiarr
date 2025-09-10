using Audiarr.Core.Entities;

namespace Audiarr.Core.DTOs;

public class QueueStateDto
{
    public string QueueId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<string> TrackIds { get; set; } = new();
    public string? CurrentTrackId { get; set; }
    public int CurrentIndex { get; set; }
    public RepeatMode RepeatMode { get; set; }
    public bool IsShuffled { get; set; }
    public int TotalTracks { get; set; }
    public string? QueueSource { get; set; }
    public DateTime LastActivity { get; set; }
    public int Version { get; set; }
}