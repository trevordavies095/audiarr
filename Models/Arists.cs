using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicServer.Models
{
    public class Artist
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string SortName { get; set; } // Used for proper sorting (e.g., "Weeknd, The")

        // Navigation property (optional)
        public List<Album> Albums { get; set; }
    }
}
