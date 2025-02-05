using Microsoft.EntityFrameworkCore;
using MusicServer.Models;

namespace MusicServer.Data
{
    public class MusicDbContext : DbContext
    {
        public DbSet<MusicTrack> MusicTracks { get; set; }

        public MusicDbContext(DbContextOptions<MusicDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // This fallback must match your connection string
                optionsBuilder.UseSqlite("Data Source=musiclibrary.db");
            }
        }
    }
}