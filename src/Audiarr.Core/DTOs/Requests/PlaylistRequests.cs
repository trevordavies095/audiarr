using System.ComponentModel.DataAnnotations;

namespace Audiarr.Core.DTOs.Requests;

public record CreatePlaylistRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public required string Name { get; init; }
    
    [StringLength(1000)]
    public string? Description { get; init; }
    
    public bool IsPublic { get; init; } = false;
    
    public List<string>? InitialTrackIds { get; init; }
}

public record UpdatePlaylistRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public required string Name { get; init; }
    
    [StringLength(1000)]
    public string? Description { get; init; }
    
    public bool IsPublic { get; init; }
}

public record AddTracksRequest
{
    [Required]
    [MinLength(1)]
    public required List<string> TrackIds { get; init; }
    
    // Optional: specify position to insert at (defaults to end)
    public int? Position { get; init; }
}

public record RemoveTracksRequest
{
    [Required]
    [MinLength(1)]
    public required List<string> TrackIds { get; init; }
}

public record ReorderTracksRequest
{
    [Required]
    public required List<TrackReorderItem> Tracks { get; init; }
}

public record TrackReorderItem
{
    [Required]
    public required string TrackId { get; init; }
    
    [Required]
    public required decimal NewPosition { get; init; }
}

public record UpdatePlaylistImageRequest
{
    [Required]
    public required string ImagePath { get; init; }
}

public record CopyPlaylistRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public required string Name { get; init; }
    
    [StringLength(1000)]
    public string? Description { get; init; }
}