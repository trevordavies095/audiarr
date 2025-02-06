using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MusicServer.Models
{

    [Index(nameof(Artist))]
    [Index(nameof(Artist), nameof(AlbumName))]
    [Index(nameof(AlbumName), nameof(TrackNumber))]
    [Index(nameof(FilePath))]
    [Index(nameof(Id))]
    public class MusicTrack
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FilePath { get; set; }

        public string? TrackTitle { get; set; }
        public string? Artist { get; set; }
        public string? AlbumName { get; set; }
        public string? AlbumArtist { get; set; }
        public int? ReleaseYear { get; set; }
        public string? Genre { get; set; }
        public int? TrackNumber { get; set; }
        public TimeSpan? Duration { get; set; }
        public string? FileFormat { get; set; }
        public int? Bitrate { get; set; }  // kbps
        public long? FileSize { get; set; }  // bytes
        public string? MusicBrainzId { get; set; }
        public string? ReleaseType { get; set; }
        public string? AlbumType { get; set; }

        public override string ToString()
        {
            return $"{TrackNumber} - {TrackTitle} - {AlbumName}";
        }
    }
}
