namespace Audiarr.Api.Models.DTOs;

public class AlbumDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ArtistId { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public int? Year { get; set; }
    public int TrackCount { get; set; }
    public string? Genre { get; set; }
    public string? CoverArtPath { get; set; }
    public DateTime? ReleaseDate { get; set; }
}