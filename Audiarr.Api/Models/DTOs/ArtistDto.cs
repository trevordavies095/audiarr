namespace Audiarr.Api.Models.DTOs;

public class ArtistDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SortName { get; set; }
    public int AlbumCount { get; set; }
    public int TrackCount { get; set; }
}