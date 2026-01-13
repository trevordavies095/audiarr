namespace Audiarr.Core.Entities;

public class Album : BaseEntity
{
    public required string Title { get; set; }
    public string? TitleNormalized { get; set; }
    public required string ArtistId { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public int? ReleaseYear { get; set; }
    public string? CoverArtPath { get; set; }
    public string? Genre { get; set; }
    public int? Year { get; set; }
    public DateTime AddedDate => CreatedAt; // Use base entity property

    // Navigation properties
    public virtual Artist Artist { get; set; } = null!;
    public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();
    public virtual ICollection<AlbumArtist> AlbumArtists { get; set; } = new List<AlbumArtist>();
    public virtual ICollection<AlbumGenre> AlbumGenres { get; set; } = new List<AlbumGenre>();
}