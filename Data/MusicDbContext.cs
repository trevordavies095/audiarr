using Microsoft.EntityFrameworkCore;
using MusicServer.Models;

namespace MusicServer.Data
{
    /// <summary>
    /// Represents the Entity Framework Core database context for the Music Server.
    /// </summary>
    public class MusicDbContext : DbContext
    {
        #region DbSets

        /// <summary>
        /// Gets or sets the server settings.
        /// </summary>
        public DbSet<ServerSettings> ServerSettings { get; set; }

        /// <summary>
        /// Gets or sets the artists.
        /// </summary>
        public DbSet<Artist> Artists { get; set; }

        /// <summary>
        /// Gets or sets the albums.
        /// </summary>
        public DbSet<Album> Albums { get; set; }

        /// <summary>
        /// Gets or sets the tracks.
        /// </summary>
        public DbSet<Track> Tracks { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MusicDbContext"/> class.
        /// </summary>
        /// <param name="options">The options to be used by the context.</param>
        public MusicDbContext(DbContextOptions<MusicDbContext> options)
            : base(options)
        {
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Configures the database (and other options) to be used for this context.
        /// </summary>
        /// <param name="optionsBuilder">A builder used to create or modify options for this context.</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Fallback connection string; must match your connection settings.
                optionsBuilder.UseSqlite("Data Source=musiclibrary.db");
            }
        }

        /// <summary>
        /// Configures the model that was discovered by convention from the entity types
        /// exposed in <see cref="DbSet{TEntity}"/> properties on this context.
        /// </summary>
        /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Seed a single row for ServerSettings (enforcing a single settings record).
            modelBuilder.Entity<ServerSettings>()
                .HasData(new ServerSettings { Id = 1, ServerName = "Audiarr" });

            // Configure indexes for Artist.
            modelBuilder.Entity<Artist>()
                .HasIndex(a => a.Name)
                .HasDatabaseName("idx_artists_name");

            modelBuilder.Entity<Artist>()
                .HasIndex(a => a.SortName)
                .HasDatabaseName("idx_artists_sortname");

            // Configure indexes for Album.
            modelBuilder.Entity<Album>()
                .HasIndex(a => new { a.Name, a.ArtistId })
                .IsUnique()
                .HasDatabaseName("idx_albums_name_artist");

            modelBuilder.Entity<Album>()
                .HasIndex(a => a.ReleaseYear)
                .HasDatabaseName("idx_albums_releaseyear");

            // Configure indexes for Track.
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

            // Configure relationships and cascade delete behavior.
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

        #endregion
    }
}
