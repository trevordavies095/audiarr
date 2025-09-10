namespace Audiarr.Core.DTOs;

public class PlaylistDetailsDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public string? ImagePath { get; set; }
    public int TrackCount { get; set; }
    public TimeSpan? TotalDuration { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public int PlayCount { get; set; }

    // Include the full list of tracks
    public List<PlaylistTrackDto> Tracks { get; set; } = new();
}