namespace Audiarr.Core.Entities;

public class Playlist : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string UserId { get; set; }
    public bool IsPublic { get; set; } = false;
    public string? ImagePath { get; set; }
    
    // New fields for enhanced playlist management
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public int PlayCount { get; set; } = 0;
    public int TrackCount { get; set; } = 0;
    public TimeSpan? TotalDuration { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
}