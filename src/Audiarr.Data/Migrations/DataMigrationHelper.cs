using System;
using System.Collections.Generic;
using System.Linq;
using Audiarr.Data.Context;
using Audiarr.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Audiarr.Data.Migrations
{
    /// <summary>
    /// Helper class for data migrations that require C# code execution.
    /// This is used by migrations that need complex logic that can't be expressed in SQL.
    /// </summary>
    public static class DataMigrationHelper
    {
        /// <summary>
        /// Migrates genre data from single-valued strings to many-to-many relationships.
        /// </summary>
        public static void MigrateGenres(AudiarrContext context, string sourceTable, string joinTable, string idColumn)
        {
            if (sourceTable == "Tracks")
            {
                MigrateTrackGenres(context);
            }
            else if (sourceTable == "Albums")
            {
                MigrateAlbumGenres(context);
            }
        }

        private static void MigrateTrackGenres(AudiarrContext context)
        {
            // Get all tracks with non-null, non-empty genre strings
            var tracksWithGenres = context.Tracks
                .Where(t => !string.IsNullOrWhiteSpace(t.Genre))
                .ToList();

            var genreCache = new Dictionary<string, string>(); // genre name -> genre id
            var trackGenresToAdd = new List<TrackGenre>();

            foreach (var track in tracksWithGenres)
            {
                var genreNames = ParseGenres(track.Genre!);
                
                foreach (var genreName in genreNames)
                {
                    // Get or create genre
                    if (!genreCache.TryGetValue(genreName, out var genreId))
                    {
                        var existingGenre = context.Genres.FirstOrDefault(g => g.Name == genreName);
                        if (existingGenre != null)
                        {
                            genreId = existingGenre.Id;
                        }
                        else
                        {
                            // Create new genre
                            var newGenre = new Genre
                            {
                                Id = Guid.NewGuid().ToString(),
                                Name = genreName,
                                NormalizedName = NormalizeString(genreName),
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            context.Genres.Add(newGenre);
                            context.SaveChanges(); // Save to get the ID
                            genreId = newGenre.Id;
                        }
                        genreCache[genreName] = genreId;
                    }

                    // Check if TrackGenre relationship already exists
                    var exists = context.TrackGenres
                        .Any(tg => tg.TrackId == track.Id && tg.GenreId == genreId);
                    
                    if (!exists)
                    {
                        trackGenresToAdd.Add(new TrackGenre
                        {
                            TrackId = track.Id,
                            GenreId = genreId
                        });
                    }
                }
            }

            // Batch insert TrackGenre entries
            if (trackGenresToAdd.Any())
            {
                context.TrackGenres.AddRange(trackGenresToAdd);
                context.SaveChanges();
            }
        }

        private static void MigrateAlbumGenres(AudiarrContext context)
        {
            // Get all albums with non-null, non-empty genre strings
            var albumsWithGenres = context.Albums
                .Where(a => !string.IsNullOrWhiteSpace(a.Genre))
                .ToList();

            var genreCache = new Dictionary<string, string>(); // genre name -> genre id
            var albumGenresToAdd = new List<AlbumGenre>();

            foreach (var album in albumsWithGenres)
            {
                var genreNames = ParseGenres(album.Genre!);
                
                foreach (var genreName in genreNames)
                {
                    // Get or create genre
                    if (!genreCache.TryGetValue(genreName, out var genreId))
                    {
                        var existingGenre = context.Genres.FirstOrDefault(g => g.Name == genreName);
                        if (existingGenre != null)
                        {
                            genreId = existingGenre.Id;
                        }
                        else
                        {
                            // Create new genre
                            var newGenre = new Genre
                            {
                                Id = Guid.NewGuid().ToString(),
                                Name = genreName,
                                NormalizedName = NormalizeString(genreName),
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            context.Genres.Add(newGenre);
                            context.SaveChanges(); // Save to get the ID
                            genreId = newGenre.Id;
                        }
                        genreCache[genreName] = genreId;
                    }

                    // Check if AlbumGenre relationship already exists
                    var exists = context.AlbumGenres
                        .Any(ag => ag.AlbumId == album.Id && ag.GenreId == genreId);
                    
                    if (!exists)
                    {
                        albumGenresToAdd.Add(new AlbumGenre
                        {
                            AlbumId = album.Id,
                            GenreId = genreId
                        });
                    }
                }
            }

            // Batch insert AlbumGenre entries
            if (albumGenresToAdd.Any())
            {
                context.AlbumGenres.AddRange(albumGenresToAdd);
                context.SaveChanges();
            }
        }

        private static List<string> ParseGenres(string genreString)
        {
            if (string.IsNullOrWhiteSpace(genreString))
                return new List<string>();
            
            // Try delimiters in order of preference: /, ;, ,
            char[] delimiters = { '/', ';', ',' };
            char delimiter = delimiters.FirstOrDefault(d => genreString.Contains(d));
            
            if (delimiter != '\0')
            {
                return genreString
                    .Split(delimiter, StringSplitOptions.RemoveEmptyEntries)
                    .Select(g => g.Trim())
                    .Where(g => !string.IsNullOrWhiteSpace(g))
                    .Distinct()
                    .ToList();
            }
            
            // No delimiter found, treat as single genre
            return new List<string> { genreString.Trim() };
        }

        private static string NormalizeString(string input)
        {
            return input.ToLowerInvariant()
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("_", "")
                .Replace(".", "")
                .Replace(",", "")
                .Replace("'", "")
                .Replace("\"", "");
        }

        /// <summary>
        /// Checks if a genre string would produce any results after parsing.
        /// Returns false for delimiter-only strings (e.g., "/", ";", ",") that would
        /// result in an empty list after parsing.
        /// </summary>
        public static bool WouldProduceGenres(string genreString)
        {
            if (string.IsNullOrWhiteSpace(genreString))
                return false;
            
            // Try delimiters in order of preference: /, ;, ,
            char[] delimiters = { '/', ';', ',' };
            char delimiter = delimiters.FirstOrDefault(d => genreString.Contains(d));
            
            if (delimiter != '\0')
            {
                // If delimiter found, check if splitting produces any non-empty results
                var parts = genreString
                    .Split(delimiter, StringSplitOptions.RemoveEmptyEntries)
                    .Select(g => g.Trim())
                    .Where(g => !string.IsNullOrWhiteSpace(g));
                
                return parts.Any();
            }
            
            // No delimiter found, treat as single genre (will produce one result)
            return !string.IsNullOrWhiteSpace(genreString.Trim());
        }
    }
}
