using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicServer.Models
{
    public class Album
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public int ArtistId { get; set; }  // Foreign Key
        public Artist Artist { get; set; } // Navigation property

        public int? ReleaseYear { get; set; }
        public string Genre { get; set; }
        public string? CoverArtUrl { get; set; }
        public DateTime DateAdded { get; set; } = DateTime.UtcNow; // Default value for new albums

        // Navigation property (optional)
        public List<Track> Tracks { get; set; }
    }
}
