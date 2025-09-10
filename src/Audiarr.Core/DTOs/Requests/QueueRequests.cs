using System.ComponentModel.DataAnnotations;
using Audiarr.Core.Entities;

namespace Audiarr.Core.DTOs.Requests;

public record AddToQueueRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one track must be provided")]
    [MaxLength(100, ErrorMessage = "Cannot add more than 100 tracks at once")]
    public required List<string> TrackIds { get; init; }
    
    [StringLength(100)]
    public string? Source { get; init; }
    
    public bool PlayNext { get; init; } = false;
}

public record UpdateQueueRequest
{
    public RepeatMode? RepeatMode { get; init; }
    
    public bool? IsShuffled { get; init; }
    
    [Range(0, int.MaxValue, ErrorMessage = "Current index must be non-negative")]
    public int? CurrentIndex { get; init; }
}

public record ReorderQueueRequest
{
    [Required]
    public required string TrackId { get; init; }
    
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "New index must be non-negative")]
    public required int NewIndex { get; init; }
}

public record ClearQueueRequest
{
    public bool KeepCurrentTrack { get; init; } = false;
}

public record ReplaceQueueRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one track must be provided")]
    [MaxLength(1000, ErrorMessage = "Queue cannot exceed 1000 tracks")]
    public required List<string> TrackIds { get; init; }
    
    [Range(0, int.MaxValue, ErrorMessage = "Start index must be non-negative")]
    public int StartIndex { get; init; } = 0;
    
    [StringLength(100)]
    public string? Source { get; init; }
}