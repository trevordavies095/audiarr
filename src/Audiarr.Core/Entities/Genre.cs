namespace Audiarr.Core.Entities;

public class Genre : BaseEntity
{
    public required string Name { get; set; }
    public string? NormalizedName { get; set; }

    // Navigation properties
    public virtual ICollection<TrackGenre> TrackGenres { get; set; } = new List<TrackGenre>();
    public virtual ICollection<AlbumGenre> AlbumGenres { get; set; } = new List<AlbumGenre>();
}
