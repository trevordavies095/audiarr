using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicServer.Models
{
    /// <summary>
    /// Represents an artist in the music library.
    /// </summary>
    public class Artist
    {
        #region Primary Key

        /// <summary>
        /// Gets or sets the unique identifier for the artist.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        #endregion

        #region Artist Details

        /// <summary>
        /// Gets or sets the name of the artist.
        /// </summary>
        [Required]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the sort name for the artist (used for proper sorting, e.g., "Weeknd, The").
        /// </summary>
        [Required]
        public string? SortName { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the collection of albums associated with the artist.
        /// </summary>
        public List<Album>? Albums { get; set; }

        #endregion
    }
}
