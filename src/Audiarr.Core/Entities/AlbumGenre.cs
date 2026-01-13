namespace Audiarr.Core.Entities;

public class AlbumGenre
{
    public required string AlbumId { get; set; }
    public required string GenreId { get; set; }

    // Navigation properties
    public virtual Album Album { get; set; } = null!;
    public virtual Genre Genre { get; set; } = null!;
}
