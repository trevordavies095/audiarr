namespace Audiarr.Api.Models.Entities;

public class PlaylistTrack
{
    public required string PlaylistId { get; set; }
    public required string TrackId { get; set; }
    public int Position { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Playlist Playlist { get; set; } = null!;
    public virtual Track Track { get; set; } = null!;
}