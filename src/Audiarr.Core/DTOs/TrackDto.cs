namespace Audiarr.Core.DTOs;

public class TrackDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ArtistId { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string AlbumId { get; set; } = string.Empty;
    public string AlbumTitle { get; set; } = string.Empty;
    public int? TrackNumber { get; set; }
    public int? DiscNumber { get; set; }
    public int DurationMs { get; set; }
    public string? Genre { get; set; }
    public int? Year { get; set; }
    public long? FileSize { get; set; }
    public int? Bitrate { get; set; }
    public string? Codec { get; set; }
    public string FilePath { get; set; } = string.Empty;
}