namespace Audiarr.Core.Entities;

public class PlaylistTrack
{
    public required string PlaylistId { get; set; }
    public required string TrackId { get; set; }
    public int Position { get; set; }
    public double PositionFloat { get; set; } = 0; // For conflict-free reordering
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public string? AddedBy { get; set; } // Username who added the track (for future collaborative features)

    // Navigation properties
    public virtual Playlist Playlist { get; set; } = null!;
    public virtual Track Track { get; set; } = null!;
}