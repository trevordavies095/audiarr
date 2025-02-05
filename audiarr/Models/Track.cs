using System.ComponentModel.DataAnnotations;

namespace MusicServer.Models
{
    public class Track
    {
        [Key]
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public int? Year { get; set; }
        public string FilePath { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{Title} - {Artist} - {Album}";
        }
    }
}

