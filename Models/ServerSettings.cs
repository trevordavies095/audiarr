using System.ComponentModel.DataAnnotations;

namespace MusicServer.Models
{
    /// <summary>
    /// Represents the server settings configuration.
    /// </summary>
    public class ServerSettings
    {
        #region Properties

        /// <summary>
        /// Gets or sets the unique identifier for the server settings.
        /// This is a single record table, always with Id = 1.
        /// </summary>
        [Key]
        public int Id { get; set; } // Single record table, always Id = 1

        /// <summary>
        /// Gets or sets the server name.
        /// Default is "Audiarr".
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ServerName { get; set; } = "Audiarr"; // Default name

        #endregion
    }
}
