namespace Audiarr.Core.Entities;

public class TrackGenre
{
    public required string TrackId { get; set; }
    public required string GenreId { get; set; }

    // Navigation properties
    public virtual Track Track { get; set; } = null!;
    public virtual Genre Genre { get; set; } = null!;
}
