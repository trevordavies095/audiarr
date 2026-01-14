using Audiarr.Core.Entities;
using Audiarr.Data.Context;

namespace Audiarr.Tests.Api;

public class TestDataBuilder
{
    private readonly AudiarrContext _context;
    private readonly Dictionary<string, Artist> _artists = new();
    private readonly Dictionary<string, Genre> _genres = new();

    public TestDataBuilder(AudiarrContext context)
    {
        _context = context;
    }

    public Artist CreateArtist(string name)
    {
        if (_artists.TryGetValue(name, out var existingArtist))
        {
            return existingArtist;
        }

        var artist = new Artist
        {
            Name = name,
            SortName = name,
            NameNormalized = name.ToLowerInvariant(),
            NormalizedName = name.ToLowerInvariant()
        };

        _context.Artists.Add(artist);
        _artists[name] = artist;
        return artist;
    }

    public Genre CreateGenre(string name)
    {
        if (_genres.TryGetValue(name, out var existingGenre))
        {
            return existingGenre;
        }

        var genre = new Genre
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant()
        };

        _context.Genres.Add(genre);
        _genres[name] = genre;
        return genre;
    }

    public Album CreateAlbum(string title, string[] artistNames, string[]? genreNames = null, int? year = null)
    {
        if (artistNames == null || artistNames.Length == 0)
        {
            throw new ArgumentException("Album must have at least one artist", nameof(artistNames));
        }

        // Create artists
        var artists = artistNames.Select(CreateArtist).ToList();
        var primaryArtist = artists[0];

        // Create album
        var album = new Album
        {
            Title = title,
            TitleNormalized = title.ToLowerInvariant(),
            ArtistId = primaryArtist.Id,
            Year = year,
            ReleaseYear = year
        };

        _context.Albums.Add(album);

        // Create album-artist relationships
        foreach (var artist in artists)
        {
            var albumArtist = new AlbumArtist
            {
                AlbumId = album.Id,
                ArtistId = artist.Id
            };
            _context.AlbumArtists.Add(albumArtist);
        }

        // Create album-genre relationships
        if (genreNames != null && genreNames.Length > 0)
        {
            var genres = genreNames.Select(CreateGenre).ToList();
            var primaryGenre = genres[0];
            album.Genre = primaryGenre.Name; // Set primary genre for backward compatibility

            foreach (var genre in genres)
            {
                var albumGenre = new AlbumGenre
                {
                    AlbumId = album.Id,
                    GenreId = genre.Id
                };
                _context.AlbumGenres.Add(albumGenre);
            }
        }

        return album;
    }

    public Track CreateTrack(string title, Album album, string[] artistNames, string[]? genreNames = null, 
        int? trackNumber = null, int? discNumber = null, int durationMs = 180000)
    {
        if (artistNames == null || artistNames.Length == 0)
        {
            throw new ArgumentException("Track must have at least one artist", nameof(artistNames));
        }

        // Create artists
        var artists = artistNames.Select(CreateArtist).ToList();
        var primaryArtist = artists[0];

        // Create track
        var track = new Track
        {
            Title = title,
            AlbumId = album.Id,
            ArtistId = primaryArtist.Id,
            TrackNumber = trackNumber,
            DiscNumber = discNumber,
            DurationMs = durationMs,
            Year = album.Year,
            FilePath = $"/test/music/{album.Title}/{title}.mp3",
            FileSizeBytes = 5000000,
            BitRate = 320,
            CodecName = "mp3"
        };

        _context.Tracks.Add(track);

        // Create track-artist relationships
        foreach (var artist in artists)
        {
            var trackArtist = new TrackArtist
            {
                TrackId = track.Id,
                ArtistId = artist.Id
            };
            _context.TrackArtists.Add(trackArtist);
        }

        // Create track-genre relationships
        if (genreNames != null && genreNames.Length > 0)
        {
            var genres = genreNames.Select(CreateGenre).ToList();
            var primaryGenre = genres[0];
            track.Genre = primaryGenre.Name; // Set primary genre for backward compatibility

            foreach (var genre in genres)
            {
                var trackGenre = new TrackGenre
                {
                    TrackId = track.Id,
                    GenreId = genre.Id
                };
                _context.TrackGenres.Add(trackGenre);
            }
        }

        return track;
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}
