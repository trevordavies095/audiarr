namespace Audiarr.Core.Entities;

public class TrackArtist
{
    public required string TrackId { get; set; }
    public required string ArtistId { get; set; }

    // Navigation properties
    public virtual Track Track { get; set; } = null!;
    public virtual Artist Artist { get; set; } = null!;
}
