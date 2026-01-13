using Microsoft.EntityFrameworkCore;
using Audiarr.Core.Entities;

namespace Audiarr.Data.Context;

public class AudiarrContext : DbContext
{
    public AudiarrContext(DbContextOptions<AudiarrContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Artist> Artists { get; set; } = null!;
    public DbSet<Genre> Genres { get; set; } = null!;
    public DbSet<Album> Albums { get; set; } = null!;
    public DbSet<Track> Tracks { get; set; } = null!;
    public DbSet<Playlist> Playlists { get; set; } = null!;
    public DbSet<PlaylistTrack> PlaylistTracks { get; set; } = null!;
    public DbSet<TrackArtist> TrackArtists { get; set; } = null!;
    public DbSet<AlbumArtist> AlbumArtists { get; set; } = null!;
    public DbSet<TrackGenre> TrackGenres { get; set; } = null!;
    public DbSet<AlbumGenre> AlbumGenres { get; set; } = null!;
    public DbSet<PlaybackQueue> PlaybackQueues { get; set; } = null!;
    public DbSet<Session> Sessions { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
        });

        // Configure Artist entity
        modelBuilder.Entity<Artist>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.NormalizedName);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SortName).HasMaxLength(200);
            entity.Property(e => e.NormalizedName).HasMaxLength(200);
        });

        // Configure Genre entity
        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.NormalizedName);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.NormalizedName).HasMaxLength(200);
        });

        // Configure Album entity
        modelBuilder.Entity<Album>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => e.ArtistId);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);

            entity.HasOne(e => e.Artist)
                .WithMany(a => a.Albums)
                .HasForeignKey(e => e.ArtistId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Track entity
        modelBuilder.Entity<Track>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => e.AlbumId);
            entity.HasIndex(e => e.ArtistId);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.FilePath).IsRequired();

            entity.HasOne(e => e.Album)
                .WithMany(a => a.Tracks)
                .HasForeignKey(e => e.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Artist)
                .WithMany(a => a.Tracks)
                .HasForeignKey(e => e.ArtistId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Playlist entity
        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Indexes for performance
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsPublic);
            entity.HasIndex(e => e.LastModified);
            entity.HasIndex(e => new { e.UserId, e.IsPublic })
                .HasDatabaseName("IX_Playlists_UserId_IsPublic");

            // Property configurations
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.PlayCount).HasDefaultValue(0);
            entity.Property(e => e.TrackCount).HasDefaultValue(0);

            // Configure TotalDuration as ticks for SQLite storage
            entity.Property(e => e.TotalDuration)
                .HasConversion(
                    v => v.HasValue ? v.Value.Ticks : (long?)null,
                    v => v.HasValue ? new TimeSpan(v.Value) : null);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Playlists)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure PlaylistTrack entity (many-to-many join table)
        modelBuilder.Entity<PlaylistTrack>(entity =>
        {
            entity.HasKey(e => new { e.PlaylistId, e.TrackId });

            // Indexes for performance
            entity.HasIndex(e => new { e.PlaylistId, e.Position });
            entity.HasIndex(e => new { e.PlaylistId, e.PositionFloat })
                .HasDatabaseName("IX_PlaylistTracks_PlaylistId_PositionFloat");
            entity.HasIndex(e => e.AddedAt);

            // Property configurations
            // PositionFloat is now double, no precision configuration needed
            entity.Property(e => e.AddedBy)
                .HasMaxLength(50);
            entity.Property(e => e.AddedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Relationships with cascade delete
            entity.HasOne(e => e.Playlist)
                .WithMany(p => p.PlaylistTracks)
                .HasForeignKey(e => e.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Track)
                .WithMany(t => t.PlaylistTracks)
                .HasForeignKey(e => e.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure TrackArtist entity (many-to-many join table)
        modelBuilder.Entity<TrackArtist>(entity =>
        {
            entity.HasKey(e => new { e.TrackId, e.ArtistId });

            // Indexes for performance
            entity.HasIndex(e => e.TrackId);
            entity.HasIndex(e => e.ArtistId);

            // Relationships with cascade delete
            entity.HasOne(e => e.Track)
                .WithMany(t => t.TrackArtists)
                .HasForeignKey(e => e.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Artist)
                .WithMany(a => a.TrackArtists)
                .HasForeignKey(e => e.ArtistId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AlbumArtist entity (many-to-many join table)
        modelBuilder.Entity<AlbumArtist>(entity =>
        {
            entity.HasKey(e => new { e.AlbumId, e.ArtistId });

            // Indexes for performance
            entity.HasIndex(e => e.AlbumId);
            entity.HasIndex(e => e.ArtistId);

            // Relationships with cascade delete
            entity.HasOne(e => e.Album)
                .WithMany(a => a.AlbumArtists)
                .HasForeignKey(e => e.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Artist)
                .WithMany(a => a.AlbumArtists)
                .HasForeignKey(e => e.ArtistId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure TrackGenre entity (many-to-many join table)
        modelBuilder.Entity<TrackGenre>(entity =>
        {
            entity.HasKey(e => new { e.TrackId, e.GenreId });

            // Indexes for performance
            entity.HasIndex(e => e.TrackId);
            entity.HasIndex(e => e.GenreId);

            // Relationships with cascade delete
            entity.HasOne(e => e.Track)
                .WithMany(t => t.TrackGenres)
                .HasForeignKey(e => e.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Genre)
                .WithMany(g => g.TrackGenres)
                .HasForeignKey(e => e.GenreId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AlbumGenre entity (many-to-many join table)
        modelBuilder.Entity<AlbumGenre>(entity =>
        {
            entity.HasKey(e => new { e.AlbumId, e.GenreId });

            // Indexes for performance
            entity.HasIndex(e => e.AlbumId);
            entity.HasIndex(e => e.GenreId);

            // Relationships with cascade delete
            entity.HasOne(e => e.Album)
                .WithMany(a => a.AlbumGenres)
                .HasForeignKey(e => e.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Genre)
                .WithMany(g => g.AlbumGenres)
                .HasForeignKey(e => e.GenreId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure PlaybackQueue entity
        modelBuilder.Entity<PlaybackQueue>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Ensure one queue per user
            entity.HasIndex(e => e.UserId).IsUnique();

            // Indexes for performance
            entity.HasIndex(e => e.LastActivity);
            entity.HasIndex(e => e.CurrentTrackId);

            // Property configurations
            entity.Property(e => e.QueueStateJson)
                .IsRequired()
                .HasDefaultValue("{}")
                .HasColumnType("TEXT");

            entity.Property(e => e.RepeatMode)
                .HasConversion<int>()
                .HasDefaultValue(RepeatMode.None);

            entity.Property(e => e.IsShuffled)
                .HasDefaultValue(false);

            entity.Property(e => e.CurrentIndex)
                .HasDefaultValue(0);

            entity.Property(e => e.Version)
                .HasDefaultValue(1);

            // Ignore the non-mapped QueueState property
            entity.Ignore(e => e.QueueState);

            // Relationships
            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<PlaybackQueue>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CurrentTrack)
                .WithMany()
                .HasForeignKey(e => e.CurrentTrackId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure Session entity
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RefreshTokenHash).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.ExpiresAt });
            entity.Property(e => e.RefreshTokenHash).IsRequired();

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AuditLog entity
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.PerformedByUserId);
            entity.HasIndex(e => e.TargetUserId);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Details).HasMaxLength(1000);

            entity.HasOne(e => e.PerformedByUser)
                .WithMany()
                .HasForeignKey(e => e.PerformedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetUser)
                .WithMany()
                .HasForeignKey(e => e.TargetUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Seed admin user with fixed values to prevent migration changes
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = "admin",
            Username = "admin",
            Email = "admin@localhost",
            PasswordHash = "$2a$11$OfpXVYD9ge7s.q0LiudGbe3AOGBaxel1f8BAGKT4pAeQEL8Hsae0m", // BCrypt hash of "changeme"
            Role = "admin",
            IsActive = true,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;
            entity.UpdatedAt = DateTime.UtcNow;

            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = DateTime.UtcNow;
            }
        }
    }
}