using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicServer.Models
{
    public class Track
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public int ArtistId { get; set; } // Foreign Key
        public Artist Artist { get; set; } // Navigation property

        public int AlbumId { get; set; } // Foreign Key
        public Album Album { get; set; }  // Navigation property

        public int TrackNumber { get; set; }
        public string Duration { get; set; } = string.Empty;
        public string FileFormat { get; set; } = string.Empty;
        public int Bitrate { get; set; }
        public long FileSize { get; set; }
        public string FilePath { get; set; } = string.Empty;

        // Stream URL will be generated in API responses
        [NotMapped]
        public string StreamUrl => $"/api/music/stream/{Id}";
    }
}
