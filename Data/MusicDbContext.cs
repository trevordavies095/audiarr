using Microsoft.EntityFrameworkCore;
using MusicServer.Models;

namespace MusicServer.Data
{
    public class MusicDbContext : DbContext
    {
        public DbSet<ServerSettings> ServerSettings { get; set; }
        public DbSet<Artist> Artists { get; set; }
        public DbSet<Album> Albums { get; set; }
        public DbSet<Track> Tracks { get; set; }

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

            base.OnModelCreating(modelBuilder);
        }
    }
}