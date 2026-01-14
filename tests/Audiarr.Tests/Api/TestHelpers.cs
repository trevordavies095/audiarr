using Audiarr.Core.DTOs;
using Xunit;

namespace Audiarr.Tests.Api;

public static class TestHelpers
{
    public static void AssertMultiValuedTags(TrackDto dto, int expectedArtistCount, int expectedGenreCount)
    {
        Assert.NotNull(dto.ArtistIds);
        Assert.NotNull(dto.ArtistNames);
        Assert.NotNull(dto.Genres);

        Assert.Equal(expectedArtistCount, dto.ArtistIds.Length);
        Assert.Equal(expectedArtistCount, dto.ArtistNames.Length);
        Assert.Equal(expectedGenreCount, dto.Genres.Length);

        // Ensure no duplicates
        Assert.Equal(dto.ArtistIds.Length, dto.ArtistIds.Distinct().Count());
        Assert.Equal(dto.ArtistNames.Length, dto.ArtistNames.Distinct().Count());
        Assert.Equal(dto.Genres.Length, dto.Genres.Distinct().Count());

        // Ensure arrays match in length
        Assert.Equal(dto.ArtistIds.Length, dto.ArtistNames.Length);
    }

    public static void AssertMultiValuedTags(AlbumDto dto, int expectedArtistCount, int expectedGenreCount)
    {
        Assert.NotNull(dto.ArtistIds);
        Assert.NotNull(dto.ArtistNames);
        Assert.NotNull(dto.Genres);

        Assert.Equal(expectedArtistCount, dto.ArtistIds.Length);
        Assert.Equal(expectedArtistCount, dto.ArtistNames.Length);
        Assert.Equal(expectedGenreCount, dto.Genres.Length);

        // Ensure no duplicates
        Assert.Equal(dto.ArtistIds.Length, dto.ArtistIds.Distinct().Count());
        Assert.Equal(dto.ArtistNames.Length, dto.ArtistNames.Distinct().Count());
        Assert.Equal(dto.Genres.Length, dto.Genres.Distinct().Count());

        // Ensure arrays match in length
        Assert.Equal(dto.ArtistIds.Length, dto.ArtistNames.Length);
    }

    public static void AssertBackwardCompatibility(TrackDto dto)
    {
        // Single-value fields should be present
        Assert.NotNull(dto.ArtistId);
        Assert.NotNull(dto.ArtistName);
        
        // If arrays have values, primary should match first element
        if (dto.ArtistIds.Length > 0)
        {
            Assert.Equal(dto.ArtistId, dto.ArtistIds[0]);
            Assert.Equal(dto.ArtistName, dto.ArtistNames[0]);
        }

        // Genre can be null, but if arrays have values, primary should match first element
        if (dto.Genres.Length > 0)
        {
            Assert.NotNull(dto.Genre);
            Assert.Equal(dto.Genre, dto.Genres[0]);
        }
        else if (dto.Genre != null)
        {
            // If genre is set but no genres array, that's also valid (backward compatibility)
            Assert.True(true);
        }

        // Alias properties should match
        Assert.Equal(dto.ArtistId, dto.PrimaryArtistId);
        Assert.Equal(dto.ArtistName, dto.PrimaryArtistName);
    }

    public static void AssertBackwardCompatibility(AlbumDto dto)
    {
        // Single-value fields should be present
        Assert.NotNull(dto.ArtistId);
        Assert.NotNull(dto.ArtistName);
        
        // If arrays have values, primary should match first element
        if (dto.ArtistIds.Length > 0)
        {
            Assert.Equal(dto.ArtistId, dto.ArtistIds[0]);
            Assert.Equal(dto.ArtistName, dto.ArtistNames[0]);
        }

        // Genre can be null, but if arrays have values, primary should match first element
        if (dto.Genres.Length > 0)
        {
            Assert.NotNull(dto.Genre);
            Assert.Equal(dto.Genre, dto.Genres[0]);
        }
        else if (dto.Genre != null)
        {
            // If genre is set but no genres array, that's also valid (backward compatibility)
            Assert.True(true);
        }

        // Alias properties should match
        Assert.Equal(dto.ArtistId, dto.PrimaryArtistId);
        Assert.Equal(dto.ArtistName, dto.PrimaryArtistName);
    }

    public static void AssertPrimaryArtistFirst(TrackDto dto, string expectedPrimaryArtistId)
    {
        if (dto.ArtistIds.Length > 0)
        {
            Assert.Equal(expectedPrimaryArtistId, dto.ArtistIds[0]);
            Assert.Equal(dto.ArtistId, dto.ArtistIds[0]);
        }
    }

    public static void AssertPrimaryArtistFirst(AlbumDto dto, string expectedPrimaryArtistId)
    {
        if (dto.ArtistIds.Length > 0)
        {
            Assert.Equal(expectedPrimaryArtistId, dto.ArtistIds[0]);
            Assert.Equal(dto.ArtistId, dto.ArtistIds[0]);
        }
    }

    public static void AssertAllArtistsIncluded(TrackDto dto, string[] expectedArtistIds)
    {
        Assert.All(expectedArtistIds, artistId => 
            Assert.Contains(artistId, dto.ArtistIds));
    }

    public static void AssertAllArtistsIncluded(AlbumDto dto, string[] expectedArtistIds)
    {
        Assert.All(expectedArtistIds, artistId => 
            Assert.Contains(artistId, dto.ArtistIds));
    }

    public static void AssertAllGenresIncluded(TrackDto dto, string[] expectedGenres)
    {
        Assert.All(expectedGenres, genre => 
            Assert.Contains(genre, dto.Genres));
    }

    public static void AssertAllGenresIncluded(AlbumDto dto, string[] expectedGenres)
    {
        Assert.All(expectedGenres, genre => 
            Assert.Contains(genre, dto.Genres));
    }
}
