using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MusicServer.Models
{
    public class ServerSettings
    {
        [Key]
        public int Id { get; set; } // Single record table, always Id = 1

        [Required]
        [MaxLength(100)]
        public string ServerName { get; set; } = "Audiarr"; // Default name
    }
}
