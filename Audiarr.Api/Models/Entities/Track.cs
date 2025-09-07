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
    public long? FileSize => FileSizeBytes; // Alias for compatibility
    public int? BitRate { get; set; }
    public int? Bitrate => BitRate; // Alias for compatibility
    public int? SampleRate { get; set; }
    public string? CodecName { get; set; }
    public string? Codec => CodecName; // Alias for compatibility
    public int? Channels { get; set; }
    public string? FileHash { get; set; }
    public DateTime AddedDate => CreatedAt; // Use base entity property
    public DateTime ModifiedDate => UpdatedAt; // Use base entity property
    public int PlayCount { get; set; }
    public DateTime? LastPlayedDate { get; set; }
    
    // Navigation properties
    public virtual Album Album { get; set; } = null!;
    public virtual Artist Artist { get; set; } = null!;
    public virtual ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
}