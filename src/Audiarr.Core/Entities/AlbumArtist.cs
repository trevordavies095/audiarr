namespace Audiarr.Core.Entities;

public class AlbumArtist
{
    public required string AlbumId { get; set; }
    public required string ArtistId { get; set; }

    // Navigation properties
    public virtual Album Album { get; set; } = null!;
    public virtual Artist Artist { get; set; } = null!;
}
