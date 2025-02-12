using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicServer.Models
{
    /// <summary>
    /// Represents a music track along with its metadata and associations.
    /// </summary>
    public class Track
    {
        #region Primary Key

        /// <summary>
        /// Gets or sets the unique identifier for the track.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        #endregion

        #region Track Details

        /// <summary>
        /// Gets or sets the title of the track.
        /// </summary>
        [Required]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the track number.
        /// </summary>
        public int TrackNumber { get; set; }

        /// <summary>
        /// Gets or sets the disc number.
        /// </summary>
        public int? DiscNumber { get; set; }

        /// <summary>
        /// Gets or sets the duration of the track.
        /// </summary>
        public string Duration { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file format of the track.
        /// </summary>
        public string FileFormat { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the audio bitrate of the track.
        /// </summary>
        public int Bitrate { get; set; }

        /// <summary>
        /// Gets or sets the file size of the track.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Gets or sets the file path of the track.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the foreign key for the associated artist.
        /// </summary>
        public int ArtistId { get; set; } // Foreign Key

        /// <summary>
        /// Gets or sets the associated artist.
        /// </summary>
        public Artist? Artist { get; set; } // Navigation property

        /// <summary>
        /// Gets or sets the foreign key for the associated album.
        /// </summary>
        public int AlbumId { get; set; } // Foreign Key

        /// <summary>
        /// Gets or sets the associated album.
        /// </summary>
        public Album? Album { get; set; }  // Navigation property

        #endregion

        #region Computed Properties

        /// <summary>
        /// Gets the URL to stream this track.
        /// </summary>
        [NotMapped]
        public string StreamUrl => $"/api/music/stream/{Id}";

        #endregion
    }
}
