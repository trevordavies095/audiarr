namespace Audiarr.Api.Models.Entities;

public class Track : BaseEntity
{
    public required string Title { get; set; }
    public required string AlbumId { get; set; }
    public required string ArtistId { get; set; }
    public required string FilePath { get; set; }
    public int DurationMs { get; set; }
    public int? TrackNumber { get; set; }
    public int? DiscNumber { get; set; }
    public string? Genre { get; set; }
    public int? Year { get; set; }
    public long FileSizeBytes { get; set; }
    public int? BitRate { get; set; }
    public int? SampleRate { get; set; }
    public string? CodecName { get; set; }
    public string? FileHash { get; set; }
    
    // Navigation properties
    public virtual Album Album { get; set; } = null!;
    public virtual Artist Artist { get; set; } = null!;
    public virtual ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
}