using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audiarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrateSingleValuedToManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrate Track Artists: Copy Track.ArtistId to TrackArtists table
            migrationBuilder.Sql(@"
                INSERT OR IGNORE INTO TrackArtists (TrackId, ArtistId)
                SELECT Id, ArtistId
                FROM Tracks
                WHERE ArtistId IS NOT NULL
            ");

            // Migrate Album Artists: Copy Album.ArtistId to AlbumArtists table
            migrationBuilder.Sql(@"
                INSERT OR IGNORE INTO AlbumArtists (AlbumId, ArtistId)
                SELECT Id, ArtistId
                FROM Albums
                WHERE ArtistId IS NOT NULL
            ");

            // Note: Genre migration with complex C# parsing logic is handled by DataMigrationHelper
            // which is executed post-migration in Program.cs. This ensures proper delimiter parsing
            // and genre creation using C# code as specified in the plan.
            // 
            // For now, we'll handle single-value genres (no delimiters) in SQL for immediate migration:
            
            // Create genres for single-value track genres (no delimiter)
            migrationBuilder.Sql(@"
                INSERT OR IGNORE INTO Genres (Id, Name, NormalizedName, CreatedAt, UpdatedAt)
                SELECT 
                    lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-' || substr('89ab', abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6))),
                    trim(Tracks.Genre),
                    lower(replace(replace(replace(replace(replace(replace(replace(trim(Tracks.Genre), ' ', ''), '-', ''), '_', ''), '.', ''), ',', ''), '''', ''), char(34), '')),
                    datetime('now'),
                    datetime('now')
                FROM Tracks
                WHERE Tracks.Genre IS NOT NULL 
                    AND trim(Tracks.Genre) != ''
                    AND instr(Tracks.Genre, '/') = 0
                    AND instr(Tracks.Genre, ';') = 0
                    AND instr(Tracks.Genre, ',') = 0
                    AND NOT EXISTS (
                        SELECT 1 FROM Genres g WHERE g.Name = trim(Tracks.Genre)
                    )
                GROUP BY trim(Tracks.Genre)
            ");

            // Create TrackGenre entries for single-value genres
            migrationBuilder.Sql(@"
                INSERT OR IGNORE INTO TrackGenres (TrackId, GenreId)
                SELECT 
                    Tracks.Id,
                    g.Id
                FROM Tracks
                INNER JOIN Genres g ON g.Name = trim(Tracks.Genre)
                WHERE Tracks.Genre IS NOT NULL 
                    AND trim(Tracks.Genre) != ''
                    AND instr(Tracks.Genre, '/') = 0
                    AND instr(Tracks.Genre, ';') = 0
                    AND instr(Tracks.Genre, ',') = 0
            ");

            // Create genres for single-value album genres (no delimiter)
            migrationBuilder.Sql(@"
                INSERT OR IGNORE INTO Genres (Id, Name, NormalizedName, CreatedAt, UpdatedAt)
                SELECT 
                    lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-' || substr('89ab', abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6))),
                    trim(Albums.Genre),
                    lower(replace(replace(replace(replace(replace(replace(replace(trim(Albums.Genre), ' ', ''), '-', ''), '_', ''), '.', ''), ',', ''), '''', ''), char(34), '')),
                    datetime('now'),
                    datetime('now')
                FROM Albums
                WHERE Albums.Genre IS NOT NULL 
                    AND trim(Albums.Genre) != ''
                    AND instr(Albums.Genre, '/') = 0
                    AND instr(Albums.Genre, ';') = 0
                    AND instr(Albums.Genre, ',') = 0
                    AND NOT EXISTS (
                        SELECT 1 FROM Genres g WHERE g.Name = trim(Albums.Genre)
                    )
                GROUP BY trim(Albums.Genre)
            ");

            // Create AlbumGenre entries for single-value genres
            migrationBuilder.Sql(@"
                INSERT OR IGNORE INTO AlbumGenres (AlbumId, GenreId)
                SELECT 
                    Albums.Id,
                    g.Id
                FROM Albums
                INNER JOIN Genres g ON g.Name = trim(Albums.Genre)
                WHERE Albums.Genre IS NOT NULL 
                    AND trim(Albums.Genre) != ''
                    AND instr(Albums.Genre, '/') = 0
                    AND instr(Albums.Genre, ';') = 0
                    AND instr(Albums.Genre, ',') = 0
            ");

            // Complex delimiter-separated genres will be handled by DataMigrationHelper
            // executed post-migration in Program.cs
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Delete all many-to-many relationship entries
            migrationBuilder.Sql("DELETE FROM TrackArtists");
            migrationBuilder.Sql("DELETE FROM AlbumArtists");
            migrationBuilder.Sql("DELETE FROM TrackGenres");
            migrationBuilder.Sql("DELETE FROM AlbumGenres");
            
            // Note: We don't delete Genre entities as they might be used by new scans
        }
    }
}
