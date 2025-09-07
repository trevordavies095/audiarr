namespace Audiarr.Api.Models.Entities;

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
    
    // Navigation properties
    public virtual Artist Artist { get; set; } = null!;
    public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();
}