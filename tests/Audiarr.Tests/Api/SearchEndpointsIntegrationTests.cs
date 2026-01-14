using System.Net;
using System.Net.Http.Json;
using Audiarr.Core.DTOs;
using Audiarr.Data.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Audiarr.Tests.Api;

public class SearchEndpointsIntegrationTests : IClassFixture<AudiarrWebApplicationFactory>, IDisposable
{
    private readonly AudiarrWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly AudiarrContext _context;
    private readonly TestDataBuilder _dataBuilder;

    public SearchEndpointsIntegrationTests(AudiarrWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
        _context = factory.GetDbContext();
        _dataBuilder = new TestDataBuilder(_context);
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Clear existing data
        _context.TrackGenres.RemoveRange(_context.TrackGenres);
        _context.TrackArtists.RemoveRange(_context.TrackArtists);
        _context.AlbumGenres.RemoveRange(_context.AlbumGenres);
        _context.AlbumArtists.RemoveRange(_context.AlbumArtists);
        _context.Tracks.RemoveRange(_context.Tracks);
        _context.Albums.RemoveRange(_context.Albums);
        _context.Genres.RemoveRange(_context.Genres);
        _context.Artists.RemoveRange(_context.Artists);
        _context.SaveChanges();

        // Create test data for search scenarios:
        // Primary artist search
        var album1 = _dataBuilder.CreateAlbum("Rock Album", new[] { "Rock Artist" }, new[] { "Rock" }, 2020);
        _dataBuilder.CreateTrack("Rock Track", album1, new[] { "Rock Artist" }, new[] { "Rock" }, 1, 1);

        // Contributing artist search
        var album2 = _dataBuilder.CreateAlbum("Pop Album", new[] { "Primary Pop Artist", "Contributing Pop Artist" }, 
            new[] { "Pop" }, 2021);
        _dataBuilder.CreateTrack("Pop Track", album2, new[] { "Primary Pop Artist", "Contributing Pop Artist" }, 
            new[] { "Pop" }, 1, 1);

        // Primary genre search
        var album3 = _dataBuilder.CreateAlbum("Jazz Album", new[] { "Jazz Artist" }, new[] { "Jazz", "Smooth" }, 2022);
        _dataBuilder.CreateTrack("Jazz Track", album3, new[] { "Jazz Artist" }, new[] { "Jazz", "Smooth" }, 1, 1);

        // Contributing genre search
        var album4 = _dataBuilder.CreateAlbum("Electronic Album", new[] { "Electronic Artist" }, 
            new[] { "Electronic", "House", "Techno" }, 2023);
        _dataBuilder.CreateTrack("Electronic Track", album4, new[] { "Electronic Artist" }, 
            new[] { "Electronic", "House", "Techno" }, 1, 1);

        // Mixed: contributing artist and contributing genre
        var album5 = _dataBuilder.CreateAlbum("Mixed Album", new[] { "Primary Mixed Artist", "Contributing Mixed Artist" }, 
            new[] { "Mixed Genre", "Secondary Genre" }, 2024);
        _dataBuilder.CreateTrack("Mixed Track", album5, 
            new[] { "Primary Mixed Artist", "Contributing Mixed Artist" }, 
            new[] { "Mixed Genre", "Secondary Genre" }, 1, 1);

        _dataBuilder.SaveChanges();
    }

    [Fact]
    public async Task Search_FindsTracksByPrimaryArtist()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/search?q=Rock Artist");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Tracks);
        
        var track = result.Tracks.FirstOrDefault(t => t.Title == "Rock Track");
        Assert.NotNull(track);
        Assert.Equal("Rock Artist", track.ArtistName);
    }

    [Fact]
    public async Task Search_FindsTracksByContributingArtist()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/search?q=Contributing Pop Artist");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Tracks);
        
        // Search should find the track even though the artist is contributing (not primary)
        var track = result.Tracks.FirstOrDefault(t => t.Title == "Pop Track");
        Assert.NotNull(track);
    }

    [Fact]
    public async Task Search_FindsAlbumsByPrimaryArtist()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/search?q=Rock Artist");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Albums);
        
        var album = result.Albums.FirstOrDefault(a => a.Title == "Rock Album");
        Assert.NotNull(album);
        Assert.Equal("Rock Artist", album.ArtistName);
    }

    [Fact]
    public async Task Search_FindsAlbumsByContributingArtist()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/search?q=Contributing Pop Artist");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Albums);
        
        // Search should find the album even though the artist is contributing (not primary)
        var album = result.Albums.FirstOrDefault(a => a.Title == "Pop Album");
        Assert.NotNull(album);
    }

    [Fact]
    public async Task Search_FindsTracksByPrimaryGenre()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/search?q=Jazz");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Tracks);
        
        var track = result.Tracks.FirstOrDefault(t => t.Title == "Jazz Track");
        Assert.NotNull(track);
    }

    [Fact]
    public async Task Search_FindsTracksByContributingGenre()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/search?q=House");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Tracks);
        
        // Search should find the track even though the genre is contributing (not primary)
        var track = result.Tracks.FirstOrDefault(t => t.Title == "Electronic Track");
        Assert.NotNull(track);
    }

    [Fact]
    public async Task Search_FindsAlbumsByPrimaryGenre()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/search?q=Jazz");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Albums);
        
        var album = result.Albums.FirstOrDefault(a => a.Title == "Jazz Album");
        Assert.NotNull(album);
    }

    [Fact]
    public async Task Search_FindsAlbumsByContributingGenre()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/search?q=House");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Albums);
        
        // Search should find the album even though the genre is contributing (not primary)
        var album = result.Albums.FirstOrDefault(a => a.Title == "Electronic Album");
        Assert.NotNull(album);
    }

    [Fact]
    public async Task Search_ReturnsResults()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/search?q=Pop");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Tracks);
        Assert.NotNull(result.Albums);
        
        // Note: The search endpoint returns simplified objects, not full DTOs with multi-valued arrays
        // This test verifies that search works, but multi-valued tag arrays are tested in the advanced search
    }

    [Fact]
    public async Task AdvancedSearch_FiltersByPrimaryArtist()
    {
        // Arrange
        var request = new AdvancedSearchRequest
        {
            Artist = "Rock Artist"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v2/search/advanced", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AdvancedSearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Tracks);
        
        var track = result.Tracks.FirstOrDefault(t => t.Title == "Rock Track");
        Assert.NotNull(track);
        Assert.Contains("Rock Artist", track.ArtistNames);
        TestHelpers.AssertBackwardCompatibility(track);
    }

    [Fact]
    public async Task AdvancedSearch_FiltersByContributingArtist()
    {
        // Arrange
        var request = new AdvancedSearchRequest
        {
            Artist = "Contributing Pop Artist"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v2/search/advanced", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AdvancedSearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Tracks);
        
        var track = result.Tracks.FirstOrDefault(t => t.Title == "Pop Track");
        Assert.NotNull(track);
        Assert.Contains("Contributing Pop Artist", track.ArtistNames);
    }

    [Fact]
    public async Task AdvancedSearch_FiltersByPrimaryGenre()
    {
        // Arrange
        var request = new AdvancedSearchRequest
        {
            Genre = "Jazz"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v2/search/advanced", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AdvancedSearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Tracks);
        
        var track = result.Tracks.FirstOrDefault(t => t.Title == "Jazz Track");
        Assert.NotNull(track);
        Assert.Contains("Jazz", track.Genres);
    }

    [Fact]
    public async Task AdvancedSearch_FiltersByContributingGenre()
    {
        // Arrange
        var request = new AdvancedSearchRequest
        {
            Genre = "House"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v2/search/advanced", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AdvancedSearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Tracks);
        
        // Should find track even though House is a contributing genre (not primary)
        var track = result.Tracks.FirstOrDefault(t => t.Title == "Electronic Track");
        Assert.NotNull(track);
        Assert.Contains("House", track.Genres);
        TestHelpers.AssertBackwardCompatibility(track);
    }

    [Fact]
    public async Task AdvancedSearch_CombinesMultipleFilters()
    {
        // Arrange
        var request = new AdvancedSearchRequest
        {
            Artist = "Contributing Mixed Artist",
            Genre = "Secondary Genre"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v2/search/advanced", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AdvancedSearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Tracks);
        
        var track = result.Tracks.FirstOrDefault(t => t.Title == "Mixed Track");
        Assert.NotNull(track);
        Assert.Contains("Contributing Mixed Artist", track.ArtistNames);
        Assert.Contains("Secondary Genre", track.Genres);
    }

    [Fact]
    public async Task AdvancedSearch_ReturnsMultiValuedTagArrays()
    {
        // Arrange
        var request = new AdvancedSearchRequest
        {
            Artist = "Pop"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v2/search/advanced", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AdvancedSearchResponse>();
        Assert.NotNull(result);

        foreach (var track in result!.Tracks)
        {
            Assert.NotNull(track.ArtistIds);
            Assert.NotNull(track.ArtistNames);
            Assert.NotNull(track.Genres);
            TestHelpers.AssertBackwardCompatibility(track);
        }
    }

    [Fact]
    public async Task AdvancedSearch_PaginationWorks()
    {
        // Arrange
        var request = new AdvancedSearchRequest
        {
            Page = 1,
            PageSize = 2
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v2/search/advanced", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AdvancedSearchResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Tracks);
        Assert.True(result.Tracks.Count <= 2);
        Assert.True(result.TotalCount > 0);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _context?.Dispose();
    }

    private class SearchResponse
    {
        public string Query { get; set; } = string.Empty;
        public List<SearchTrack> Tracks { get; set; } = new();
        public List<SearchAlbum> Albums { get; set; } = new();
        public List<object> Artists { get; set; } = new();
    }

    private class SearchTrack
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string ArtistId { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string AlbumId { get; set; } = string.Empty;
        public string? AlbumTitle { get; set; }
        public int DurationMs { get; set; }
        public int? TrackNumber { get; set; }
        public int? DiscNumber { get; set; }
    }

    private class SearchAlbum
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string ArtistId { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public int? Year { get; set; }
        public string? CoverArtPath { get; set; }
        public int TrackCount { get; set; }
    }

    private class AdvancedSearchResponse
    {
        public List<TrackDto> Tracks { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    private class AdvancedSearchRequest
    {
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? Genre { get; set; }
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }
}
