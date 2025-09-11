namespace Audiarr.Core.DTOs;

public class PlaylistTrackDto
{
    public string TrackId { get; set; } = string.Empty;
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
    public string FilePath { get; set; } = string.Empty;

    // Playlist-specific fields
    public int Position { get; set; }
    public double PositionFloat { get; set; }
    public DateTime AddedAt { get; set; }
    public string? AddedBy { get; set; }
}