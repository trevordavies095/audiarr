namespace Audiarr.Api.Models.Entities;

public class Artist : BaseEntity
{
    public required string Name { get; set; }
    public string? SortName { get; set; }
    public string? NormalizedName { get; set; }
    public string? ImagePath { get; set; }
    
    // Navigation properties
    public virtual ICollection<Album> Albums { get; set; } = new List<Album>();
    public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();
}