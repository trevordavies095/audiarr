namespace Audiarr.Core.DTOs;

public class QueueItemDto
{
    public int Index { get; set; }
    public string TrackId { get; set; } = string.Empty;
    public TrackDto Track { get; set; } = null!;
    public DateTime AddedAt { get; set; }
    public string? Source { get; set; }
}