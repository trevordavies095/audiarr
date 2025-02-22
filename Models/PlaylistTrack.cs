using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MusicServer.Models;

public class PlaylistTrack
{
    [Required]
    public int PlaylistId { get; set; }

    [Required]
    public int TrackId { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("PlaylistId")]
    public Playlist Playlist { get; set; }

    [ForeignKey("TrackId")]
    public Track Track { get; set; }
}
