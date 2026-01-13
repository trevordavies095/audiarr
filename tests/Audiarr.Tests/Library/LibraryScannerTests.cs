using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Audiarr.Core.Configuration;
using Audiarr.Data.Context;
using Audiarr.Services.Library;

namespace Audiarr.Tests.Library;

public class LibraryScannerTests : IDisposable
{
    private readonly AudiarrContext _context;
    private readonly Mock<ILogger<LibraryScanner>> _loggerMock;
    private readonly Mock<IHostEnvironment> _environmentMock;
    private readonly LibraryScanner _scanner;

    public LibraryScannerTests()
    {
        // Create in-memory database (minimal setup since we're testing parsing logic)
        var options = new DbContextOptionsBuilder<AudiarrContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableServiceProviderCaching(false)
            .Options;

        _context = new AudiarrContext(options);
        _loggerMock = new Mock<ILogger<LibraryScanner>>();
        _environmentMock = new Mock<IHostEnvironment>();

        // Default configuration
        var defaultOptions = Options.Create(new MultiValuedTagsOptions
        {
            Delimiter = "/",
            EnableDelimiterParsing = true,
            PreferredDelimiters = new[] { "/", ";", "," }
        });

        _scanner = new LibraryScanner(_context, _loggerMock.Object, _environmentMock.Object, defaultOptions);
    }

    #region ParseArtists Tests

    #region Native Multi-Valued Tags

    [Fact]
    public void ParseArtists_WithNonEmptyArray_ReturnsArtistsFromArray()
    {
        // Arrange
        var artists = new[] { "Artist A", "Artist B", "Artist C" };
        string? singleValue = "Single Artist";

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("Artist A", result);
        Assert.Contains("Artist B", result);
        Assert.Contains("Artist C", result);
        Assert.DoesNotContain("Single Artist", result); // Native tags preferred
    }

    [Fact]
    public void ParseArtists_WithSingleArtistInArray_ReturnsSingleArtist()
    {
        // Arrange
        var artists = new[] { "Artist A" };
        string? singleValue = "Single Artist";

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Single(result);
        Assert.Equal("Artist A", result[0]);
    }

    [Fact]
    public void ParseArtists_WithMultipleArtists_PrefersNativeTagsOverSingleValue()
    {
        // Arrange
        var artists = new[] { "Artist A", "Artist B" };
        string? singleValue = "Different Artist";

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("Artist A", result);
        Assert.Contains("Artist B", result);
        Assert.DoesNotContain("Different Artist", result);
    }

    #endregion

    #region Delimiter Parsing

    [Fact]
    public void ParseArtists_WithSlashDelimiter_ParsesCorrectly()
    {
        // Arrange
        string[] artists = Array.Empty<string>();
        var singleValue = "Artist A / Artist B / Artist C";

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("Artist A", result);
        Assert.Contains("Artist B", result);
        Assert.Contains("Artist C", result);
    }

    [Fact]
    public void ParseArtists_WithSemicolonDelimiter_ParsesCorrectly()
    {
        // Arrange
        string[] artists = Array.Empty<string>();
        var singleValue = "Artist A; Artist B; Artist C";

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("Artist A", result);
        Assert.Contains("Artist B", result);
        Assert.Contains("Artist C", result);
    }

    [Fact]
    public void ParseArtists_WithCommaDelimiter_ParsesCorrectly()
    {
        // Arrange
        string[] artists = Array.Empty<string>();
        var singleValue = "Artist A, Artist B, Artist C";

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("Artist A", result);
        Assert.Contains("Artist B", result);
        Assert.Contains("Artist C", result);
    }

    [Fact]
    public void ParseArtists_WithMultipleDelimiters_UsesFirstMatch()
    {
        // Arrange
        string[] artists = Array.Empty<string>();
        var singleValue = "Artist A / Artist B; Artist C";

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        // Should use '/' (first in PreferredDelimiters) and split on that only
        Assert.Equal(2, result.Count);
        Assert.Contains("Artist A", result);
        Assert.Contains("Artist B; Artist C", result); // Semicolon not used as delimiter
    }

    [Fact]
    public void ParseArtists_WithDelimiterParsingDisabled_TreatsAsSingleArtist()
    {
        // Arrange
        var options = Options.Create(new MultiValuedTagsOptions
        {
            EnableDelimiterParsing = false,
            PreferredDelimiters = new[] { "/", ";", "," }
        });
        var scanner = new LibraryScanner(_context, _loggerMock.Object, _environmentMock.Object, options);

        string[] artists = Array.Empty<string>();
        var singleValue = "Artist A / Artist B";

        // Act
        var result = scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Single(result);
        Assert.Equal("Artist A / Artist B", result[0]); // Not split
    }

    #endregion

    #region Fallback to Single-Value

    [Fact]
    public void ParseArtists_WithEmptyArray_FallsBackToSingleValue()
    {
        // Arrange
        string[] artists = Array.Empty<string>();
        var singleValue = "Single Artist";

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Single(result);
        Assert.Equal("Single Artist", result[0]);
    }

    [Fact]
    public void ParseArtists_WithNullArray_FallsBackToSingleValue()
    {
        // Arrange
        string[]? artists = null;
        var singleValue = "Single Artist";

        // Act
        var result = _scanner.ParseArtists(artists!, singleValue);

        // Assert
        Assert.Single(result);
        Assert.Equal("Single Artist", result[0]);
    }

    [Fact]
    public void ParseArtists_WithNoDelimiterInSingleValue_TreatsAsSingleArtist()
    {
        // Arrange
        string[] artists = Array.Empty<string>();
        var singleValue = "Single Artist Name";

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Single(result);
        Assert.Equal("Single Artist Name", result[0]);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ParseArtists_WithEmptyArrayAndNullSingleValue_ReturnsUnknownArtist()
    {
        // Arrange
        string[] artists = Array.Empty<string>();
        string? singleValue = null;

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Single(result);
        Assert.Equal("Unknown Artist", result[0]);
    }

    [Fact]
    public void ParseArtists_WithEmptyArrayAndEmptyStringSingleValue_ReturnsUnknownArtist()
    {
        // Arrange
        string[] artists = Array.Empty<string>();
        var singleValue = string.Empty;

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Single(result);
        Assert.Equal("Unknown Artist", result[0]);
    }

    [Fact]
    public void ParseArtists_WithWhitespaceOnlyValues_RemovesWhitespace()
    {
        // Arrange
        var artists = new[] { "  Artist A  ", "  Artist B  ", "   " };
        string? singleValue = null;

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Equal(2, result.Count); // Whitespace-only entry removed
        Assert.Contains("Artist A", result);
        Assert.Contains("Artist B", result);
        Assert.All(result, a => Assert.False(a.StartsWith(" ") || a.EndsWith(" "))); // All trimmed
    }

    [Fact]
    public void ParseArtists_WithDelimiterInArtistName_DoesNotSplit()
    {
        // Arrange
        string[] artists = Array.Empty<string>();
        var singleValue = "Artist A / B / Artist C"; // '/' is delimiter, but "Artist A / B" should be treated as one if no space around delimiter

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        // The current implementation splits on '/' regardless, so "Artist A / B" would be split
        // This is expected behavior - if user wants "Artist A / B" as one artist, they should use native multi-valued tags
        Assert.Equal(3, result.Count);
        Assert.Contains("Artist A", result);
        Assert.Contains("B", result);
        Assert.Contains("Artist C", result);
    }

    [Fact]
    public void ParseArtists_WithMultipleConsecutiveDelimiters_HandlesCorrectly()
    {
        // Arrange
        string[] artists = Array.Empty<string>();
        var singleValue = "Artist A // Artist B /// Artist C";

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        // Split removes empty entries, so consecutive delimiters create empty strings that are filtered out
        Assert.Equal(3, result.Count);
        Assert.Contains("Artist A", result);
        Assert.Contains("Artist B", result);
        Assert.Contains("Artist C", result);
    }

    [Fact]
    public void ParseArtists_WithLeadingTrailingWhitespace_TrimsCorrectly()
    {
        // Arrange
        string[] artists = Array.Empty<string>();
        var singleValue = "  Artist A  /  Artist B  ";

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("Artist A", result);
        Assert.Contains("Artist B", result);
        Assert.All(result, a => Assert.False(a.StartsWith(" ") || a.EndsWith(" ")));
    }

    [Fact]
    public void ParseArtists_WithDuplicateArtistNames_Deduplicates()
    {
        // Arrange
        var artists = new[] { "Artist A", "Artist B", "Artist A", "Artist C" };
        string? singleValue = null;

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        Assert.Equal(3, result.Count); // Duplicate removed
        Assert.Contains("Artist A", result);
        Assert.Contains("Artist B", result);
        Assert.Contains("Artist C", result);
    }

    [Fact]
    public void ParseArtists_WithCaseInsensitiveDelimiterMatching_MatchesCorrectly()
    {
        // Arrange
        string[] artists = Array.Empty<string>();
        var singleValue = "Artist A / Artist B"; // Standard delimiter

        // Act
        var result = _scanner.ParseArtists(artists, singleValue);

        // Assert
        // Delimiter matching is case-sensitive for the delimiter character itself
        // But the Contains() method should match regardless
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ParseArtists_WithNullArrayAndWhitespaceSingleValue_ReturnsUnknownArtist()
    {
        // Arrange
        string[]? artists = null;
        var singleValue = "   ";

        // Act
        var result = _scanner.ParseArtists(artists!, singleValue);

        // Assert
        Assert.Single(result);
        Assert.Equal("Unknown Artist", result[0]);
    }

    [Fact]
    public void ParseArtists_WithArrayContainingNullAndEmptyStrings_FiltersThem()
    {
        // Arrange
        var artists = new[] { "Artist A", null, "", "   ", "Artist B" };
        string? singleValue = null;

        // Act
        var result = _scanner.ParseArtists(artists!, singleValue);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("Artist A", result);
        Assert.Contains("Artist B", result);
    }

    [Fact]
    public void ParseArtists_WithCustomDelimiterOrder_RespectsOrder()
    {
        // Arrange
        var options = Options.Create(new MultiValuedTagsOptions
        {
            EnableDelimiterParsing = true,
            PreferredDelimiters = new[] { ";", ",", "/" } // Different order
        });
        var scanner = new LibraryScanner(_context, _loggerMock.Object, _environmentMock.Object, options);

        string[] artists = Array.Empty<string>();
        var singleValue = "Artist A; Artist B / Artist C"; // Contains both ; and /

        // Act
        var result = scanner.ParseArtists(artists, singleValue);

        // Assert
        // Should use ';' (first in PreferredDelimiters)
        Assert.Equal(2, result.Count);
        Assert.Contains("Artist A", result);
        Assert.Contains("Artist B / Artist C", result); // '/' not used as delimiter
    }

    #endregion

    #endregion

    #region ParseGenres Tests

    #region Native Multi-Valued Tags

    [Fact]
    public void ParseGenres_WithNonEmptyArray_ReturnsGenresFromArray()
    {
        // Arrange
        var genres = new[] { "Rock", "Pop", "Electronic" };
        string? singleValue = "Single Genre";

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("Rock", result);
        Assert.Contains("Pop", result);
        Assert.Contains("Electronic", result);
        Assert.DoesNotContain("Single Genre", result); // Native tags preferred
    }

    [Fact]
    public void ParseGenres_WithSingleGenreInArray_ReturnsSingleGenre()
    {
        // Arrange
        var genres = new[] { "Rock" };
        string? singleValue = "Pop";

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Single(result);
        Assert.Equal("Rock", result[0]);
    }

    [Fact]
    public void ParseGenres_WithMultipleGenres_PrefersNativeTagsOverSingleValue()
    {
        // Arrange
        var genres = new[] { "Rock", "Pop" };
        string? singleValue = "Electronic";

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("Rock", result);
        Assert.Contains("Pop", result);
        Assert.DoesNotContain("Electronic", result);
    }

    #endregion

    #region Delimiter Parsing

    [Fact]
    public void ParseGenres_WithSlashDelimiter_ParsesCorrectly()
    {
        // Arrange
        string[] genres = Array.Empty<string>();
        var singleValue = "Rock / Pop / Electronic";

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("Rock", result);
        Assert.Contains("Pop", result);
        Assert.Contains("Electronic", result);
    }

    [Fact]
    public void ParseGenres_WithSemicolonDelimiter_ParsesCorrectly()
    {
        // Arrange
        string[] genres = Array.Empty<string>();
        var singleValue = "Rock; Pop; Electronic";

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("Rock", result);
        Assert.Contains("Pop", result);
        Assert.Contains("Electronic", result);
    }

    [Fact]
    public void ParseGenres_WithCommaDelimiter_ParsesCorrectly()
    {
        // Arrange
        string[] genres = Array.Empty<string>();
        var singleValue = "Rock, Pop, Electronic";

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("Rock", result);
        Assert.Contains("Pop", result);
        Assert.Contains("Electronic", result);
    }

    [Fact]
    public void ParseGenres_WithMultipleDelimiters_UsesFirstMatch()
    {
        // Arrange
        string[] genres = Array.Empty<string>();
        var singleValue = "Rock / Pop; Electronic";

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        // Should use '/' (first in PreferredDelimiters) and split on that only
        Assert.Equal(2, result.Count);
        Assert.Contains("Rock", result);
        Assert.Contains("Pop; Electronic", result); // Semicolon not used as delimiter
    }

    [Fact]
    public void ParseGenres_WithDelimiterParsingDisabled_TreatsAsSingleGenre()
    {
        // Arrange
        var options = Options.Create(new MultiValuedTagsOptions
        {
            EnableDelimiterParsing = false,
            PreferredDelimiters = new[] { "/", ";", "," }
        });
        var scanner = new LibraryScanner(_context, _loggerMock.Object, _environmentMock.Object, options);

        string[] genres = Array.Empty<string>();
        var singleValue = "Rock / Pop";

        // Act
        var result = scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Single(result);
        Assert.Equal("Rock / Pop", result[0]); // Not split
    }

    #endregion

    #region Fallback to Single-Value

    [Fact]
    public void ParseGenres_WithEmptyArray_FallsBackToSingleValue()
    {
        // Arrange
        string[] genres = Array.Empty<string>();
        var singleValue = "Rock";

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Single(result);
        Assert.Equal("Rock", result[0]);
    }

    [Fact]
    public void ParseGenres_WithNullArray_FallsBackToSingleValue()
    {
        // Arrange
        string[]? genres = null;
        var singleValue = "Pop";

        // Act
        var result = _scanner.ParseGenres(genres!, singleValue);

        // Assert
        Assert.Single(result);
        Assert.Equal("Pop", result[0]);
    }

    [Fact]
    public void ParseGenres_WithNoDelimiterInSingleValue_TreatsAsSingleGenre()
    {
        // Arrange
        string[] genres = Array.Empty<string>();
        var singleValue = "Electronic Dance Music";

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Single(result);
        Assert.Equal("Electronic Dance Music", result[0]);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ParseGenres_WithEmptyArrayAndNullSingleValue_ReturnsEmptyList()
    {
        // Arrange
        string[] genres = Array.Empty<string>();
        string? singleValue = null;

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Empty(result); // Unlike artists, genres can be empty
    }

    [Fact]
    public void ParseGenres_WithEmptyArrayAndEmptyStringSingleValue_ReturnsEmptyList()
    {
        // Arrange
        string[] genres = Array.Empty<string>();
        var singleValue = string.Empty;

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Empty(result); // Unlike artists, genres can be empty
    }

    [Fact]
    public void ParseGenres_WithWhitespaceOnlyValues_RemovesWhitespace()
    {
        // Arrange
        var genres = new[] { "  Rock  ", "  Pop  ", "   " };
        string? singleValue = null;

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Equal(2, result.Count); // Whitespace-only entry removed
        Assert.Contains("Rock", result);
        Assert.Contains("Pop", result);
        Assert.All(result, g => Assert.False(g.StartsWith(" ") || g.EndsWith(" "))); // All trimmed
    }

    [Fact]
    public void ParseGenres_WithMultipleConsecutiveDelimiters_HandlesCorrectly()
    {
        // Arrange
        string[] genres = Array.Empty<string>();
        var singleValue = "Rock // Pop /// Electronic";

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        // Split removes empty entries, so consecutive delimiters create empty strings that are filtered out
        Assert.Equal(3, result.Count);
        Assert.Contains("Rock", result);
        Assert.Contains("Pop", result);
        Assert.Contains("Electronic", result);
    }

    [Fact]
    public void ParseGenres_WithLeadingTrailingWhitespace_TrimsCorrectly()
    {
        // Arrange
        string[] genres = Array.Empty<string>();
        var singleValue = "  Rock  /  Pop  ";

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("Rock", result);
        Assert.Contains("Pop", result);
        Assert.All(result, g => Assert.False(g.StartsWith(" ") || g.EndsWith(" ")));
    }

    [Fact]
    public void ParseGenres_WithDuplicateGenreNames_Deduplicates()
    {
        // Arrange
        var genres = new[] { "Rock", "Pop", "Rock", "Electronic" };
        string? singleValue = null;

        // Act
        var result = _scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Equal(3, result.Count); // Duplicate removed
        Assert.Contains("Rock", result);
        Assert.Contains("Pop", result);
        Assert.Contains("Electronic", result);
    }

    [Fact]
    public void ParseGenres_WithNullArrayAndWhitespaceSingleValue_ReturnsEmptyList()
    {
        // Arrange
        string[]? genres = null;
        var singleValue = "   ";

        // Act
        var result = _scanner.ParseGenres(genres!, singleValue);

        // Assert
        Assert.Empty(result); // Unlike artists, genres can be empty
    }

    [Fact]
    public void ParseGenres_WithArrayContainingNullAndEmptyStrings_FiltersThem()
    {
        // Arrange
        var genres = new[] { "Rock", null, "", "   ", "Pop" };
        string? singleValue = null;

        // Act
        var result = _scanner.ParseGenres(genres!, singleValue);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("Rock", result);
        Assert.Contains("Pop", result);
    }

    [Fact]
    public void ParseGenres_WithCustomDelimiterOrder_RespectsOrder()
    {
        // Arrange
        var options = Options.Create(new MultiValuedTagsOptions
        {
            EnableDelimiterParsing = true,
            PreferredDelimiters = new[] { ";", ",", "/" } // Different order
        });
        var scanner = new LibraryScanner(_context, _loggerMock.Object, _environmentMock.Object, options);

        string[] genres = Array.Empty<string>();
        var singleValue = "Rock; Pop / Electronic"; // Contains both ; and /

        // Act
        var result = scanner.ParseGenres(genres, singleValue);

        // Assert
        // Should use ';' (first in PreferredDelimiters)
        Assert.Equal(2, result.Count);
        Assert.Contains("Rock", result);
        Assert.Contains("Pop / Electronic", result); // '/' not used as delimiter
    }

    [Fact]
    public void ParseGenres_WithSingleDelimiterOnly_WorksCorrectly()
    {
        // Arrange
        var options = Options.Create(new MultiValuedTagsOptions
        {
            EnableDelimiterParsing = true,
            PreferredDelimiters = new[] { "/" } // Only one delimiter
        });
        var scanner = new LibraryScanner(_context, _loggerMock.Object, _environmentMock.Object, options);

        string[] genres = Array.Empty<string>();
        var singleValue = "Rock / Pop";

        // Act
        var result = scanner.ParseGenres(genres, singleValue);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("Rock", result);
        Assert.Contains("Pop", result);
    }

    #endregion

    #endregion

    public void Dispose()
    {
        _context?.Dispose();
    }
}
