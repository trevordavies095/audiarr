namespace Audiarr.Core.DTOs;

/// <summary>
/// Data transfer object for album information.
/// Supports both single-valued and multi-valued artists and genres for backward compatibility.
/// </summary>
public class AlbumDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Primary artist ID (first artist in the album's artist list).
    /// Maintained for backward compatibility. Use <see cref="ArtistIds"/> for all artists.
    /// </summary>
    public string ArtistId { get; set; } = string.Empty;
    
    /// <summary>
    /// Primary artist name (first artist in the album's artist list).
    /// Maintained for backward compatibility. Use <see cref="ArtistNames"/> for all artists.
    /// </summary>
    public string ArtistName { get; set; } = string.Empty;
    
    public int? Year { get; set; }
    public int TrackCount { get; set; }
    
    /// <summary>
    /// Primary genre (first genre in the album's genre list).
    /// Maintained for backward compatibility. Use <see cref="Genres"/> for all genres.
    /// </summary>
    public string? Genre { get; set; }
    
    public string? CoverArtPath { get; set; }
    public DateTime? ReleaseDate { get; set; }
    
    /// <summary>
    /// Array of all artist IDs associated with this album.
    /// Empty array if no artists are associated. The first element corresponds to the primary artist (<see cref="ArtistId"/>).
    /// </summary>
    public string[] ArtistIds { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Array of all artist names associated with this album.
    /// Empty array if no artists are associated. The first element corresponds to the primary artist (<see cref="ArtistName"/>).
    /// </summary>
    public string[] ArtistNames { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Array of all genre names associated with this album.
    /// Empty array if no genres are associated. The first element corresponds to the primary genre (<see cref="Genre"/>).
    /// </summary>
    public string[] Genres { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Alias for <see cref="ArtistId"/>. Returns the primary artist ID for backward compatibility.
    /// This property provides explicit naming to indicate it represents the primary (first) artist.
    /// </summary>
    public string PrimaryArtistId => ArtistId;
    
    /// <summary>
    /// Alias for <see cref="ArtistName"/>. Returns the primary artist name for backward compatibility.
    /// This property provides explicit naming to indicate it represents the primary (first) artist.
    /// </summary>
    public string PrimaryArtistName => ArtistName;
}