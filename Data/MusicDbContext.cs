using Microsoft.EntityFrameworkCore;
using MusicServer.Models;

namespace MusicServer.Data
{
    public class MusicDbContext : DbContext
    {
        public DbSet<MusicTrack> MusicTracks { get; set; }
        public DbSet<ServerSettings> ServerSettings { get; set; }

        public MusicDbContext(DbContextOptions<MusicDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // This fallback must match your connection string
                optionsBuilder.UseSqlite("Data Source=musiclibrary.db");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Ensure only one row exists in ServerSettings (enforce single settings record)
            modelBuilder.Entity<ServerSettings>().HasData(new ServerSettings { Id = 1, ServerName = "Audiarr" });
        }
    }
}