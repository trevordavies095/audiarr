using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicServer.Models
{
    /// <summary>
    /// Represents an album in the music library.
    /// </summary>
    public class Album
    {
        #region Primary Key

        /// <summary>
        /// Gets or sets the unique identifier for the album.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        #endregion

        #region Album Details

        /// <summary>
        /// Gets or sets the name of the album.
        /// </summary>
        [Required]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the release year of the album.
        /// </summary>
        public int? ReleaseYear { get; set; }

        /// <summary>
        /// Gets or sets the genre of the album.
        /// </summary>
        /// 
        public string? Genre { get; set; }

        /// <summary>
        /// Gets or sets the cover art URL for the album.
        /// </summary>
        /// 
        public string? CoverArtUrl { get; set; }

        /// <summary>
        /// Gets or sets the date when the album was added.
        /// </summary>
        public DateTime DateAdded { get; set; } = DateTime.UtcNow; // Default value for new albums

        #endregion

        #region Navigation Properties
        
        /// <summary>
        /// Gets or sets the foreign key referencing the associated artist.
        /// </summary>
        public int ArtistId { get; set; }  // Foreign Key

        /// <summary>
        /// Gets or sets the associated artist.
        /// </summary>
        public Artist? Artist { get; set; } // Navigation property

        /// <summary>
        /// Gets or sets the collection of tracks in the album.
        /// </summary>
        public List<Track>? Tracks { get; set; } // Navigation property (optional)

        #endregion
    }
}
