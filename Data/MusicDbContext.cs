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

            modelBuilder.Entity<Artist>()
            .HasIndex(a => a.Name)
            .HasDatabaseName("idx_artists_name");

        modelBuilder.Entity<Artist>()
            .HasIndex(a => a.SortName)
            .HasDatabaseName("idx_artists_sortname");

        modelBuilder.Entity<Album>()
            .HasIndex(a => new { a.Name, a.ArtistId })
            .IsUnique()
            .HasDatabaseName("idx_albums_name_artist");

        modelBuilder.Entity<Album>()
            .HasIndex(a => a.ReleaseYear)
            .HasDatabaseName("idx_albums_releaseyear");

        modelBuilder.Entity<Track>()
            .HasIndex(t => t.AlbumId)
            .HasDatabaseName("idx_tracks_album");

        modelBuilder.Entity<Track>()
            .HasIndex(t => t.ArtistId)
            .HasDatabaseName("idx_tracks_artist");

        modelBuilder.Entity<Track>()
            .HasIndex(t => t.FilePath)
            .HasDatabaseName("idx_tracks_filename");

        modelBuilder.Entity<Track>()
            .HasIndex(t => t.Title)
            .HasDatabaseName("idx_tracks_title");

        modelBuilder.Entity<Track>()
            .HasIndex(t => t.Id)
            .HasDatabaseName("idx_tracks_stream");

        modelBuilder.Entity<Album>()
            .HasOne(a => a.Artist)
            .WithMany()
            .HasForeignKey(a => a.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Track>()
            .HasOne(t => t.Album)
            .WithMany()
            .HasForeignKey(t => t.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Track>()
            .HasOne(t => t.Artist)
            .WithMany()
            .HasForeignKey(t => t.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }
    }
}