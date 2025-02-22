using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public class Playlist
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; }

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateModified { get; set; } = DateTime.UtcNow;

    public ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
}
