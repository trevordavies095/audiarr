using System.Net;
using System.Net.Http.Json;
using Audiarr.Core.DTOs;
using Audiarr.Data.Context;
using Xunit;

namespace Audiarr.Tests.Api;

public class AlbumEndpointsIntegrationTests : IClassFixture<AudiarrWebApplicationFactory>, IDisposable
{
    private readonly AudiarrWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly AudiarrContext _context;
    private readonly TestDataBuilder _dataBuilder;

    public AlbumEndpointsIntegrationTests(AudiarrWebApplicationFactory factory)
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

        // Create test data with various scenarios
        // Single artist, single genre
        var album1 = _dataBuilder.CreateAlbum("Test Album 1", new[] { "Artist A" }, new[] { "Rock" }, 2020);
        _dataBuilder.CreateTrack("Track 1", album1, new[] { "Artist A" }, new[] { "Rock" }, 1, 1);
        
        // Multiple artists, single genre
        var album2 = _dataBuilder.CreateAlbum("Test Album 2", new[] { "Artist B", "Artist C" }, new[] { "Pop" }, 2021);
        _dataBuilder.CreateTrack("Track 1", album2, new[] { "Artist B", "Artist C" }, new[] { "Pop" }, 1, 1);
        
        // Single artist, multiple genres
        var album3 = _dataBuilder.CreateAlbum("Test Album 3", new[] { "Artist D" }, new[] { "Electronic", "House" }, 2022);
        _dataBuilder.CreateTrack("Track 1", album3, new[] { "Artist D" }, new[] { "Electronic", "House" }, 1, 1);
        
        // Multiple artists, multiple genres
        var album4 = _dataBuilder.CreateAlbum("Test Album 4", new[] { "Artist E", "Artist F", "Artist G" }, 
            new[] { "Jazz", "Fusion", "Smooth" }, 2023);
        _dataBuilder.CreateTrack("Track 1", album4, 
            new[] { "Artist E", "Artist F", "Artist G" }, 
            new[] { "Jazz", "Fusion", "Smooth" }, 1, 1);
        
        // Single artist, no genre
        var album5 = _dataBuilder.CreateAlbum("Test Album 5", new[] { "Artist H" }, null, 2024);
        _dataBuilder.CreateTrack("Track 1", album5, new[] { "Artist H" }, null, 1, 1);
        
        _dataBuilder.SaveChanges();
    }

    [Fact]
    public async Task GetAlbums_ReturnsMultiValuedTagArrays()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlbumsResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Count > 0);

        // Check that all albums have arrays
        foreach (var album in result.Data)
        {
            Assert.NotNull(album.ArtistIds);
            Assert.NotNull(album.ArtistNames);
            Assert.NotNull(album.Genres);
        }
    }

    [Fact]
    public async Task GetAlbums_ReturnsBackwardCompatibleFields()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlbumsResponse>();
        Assert.NotNull(result);

        // Check backward compatibility
        foreach (var album in result!.Data)
        {
            TestHelpers.AssertBackwardCompatibility(album);
        }
    }

    [Fact]
    public async Task GetAlbums_AlbumWithSingleArtist_ReturnsCorrectArrays()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlbumsResponse>();
        Assert.NotNull(result);

        var album1 = result!.Data.FirstOrDefault(a => a.Title == "Test Album 1");
        Assert.NotNull(album1);
        TestHelpers.AssertMultiValuedTags(album1, 1, 1);
        TestHelpers.AssertBackwardCompatibility(album1);
    }

    [Fact]
    public async Task GetAlbums_AlbumWithMultipleArtists_ReturnsCorrectArrays()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlbumsResponse>();
        Assert.NotNull(result);

        var album2 = result!.Data.FirstOrDefault(a => a.Title == "Test Album 2");
        Assert.NotNull(album2);
        TestHelpers.AssertMultiValuedTags(album2, 2, 1);
        TestHelpers.AssertBackwardCompatibility(album2);
        Assert.Equal(2, album2.ArtistIds.Length);
    }

    [Fact]
    public async Task GetAlbums_AlbumWithMultipleGenres_ReturnsCorrectArrays()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlbumsResponse>();
        Assert.NotNull(result);

        var album3 = result!.Data.FirstOrDefault(a => a.Title == "Test Album 3");
        Assert.NotNull(album3);
        TestHelpers.AssertMultiValuedTags(album3, 1, 2);
        TestHelpers.AssertBackwardCompatibility(album3);
        Assert.Equal(2, album3.Genres.Length);
    }

    [Fact]
    public async Task GetAlbums_AlbumWithMultipleArtistsAndGenres_ReturnsCorrectArrays()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlbumsResponse>();
        Assert.NotNull(result);

        var album4 = result!.Data.FirstOrDefault(a => a.Title == "Test Album 4");
        Assert.NotNull(album4);
        TestHelpers.AssertMultiValuedTags(album4, 3, 3);
        TestHelpers.AssertBackwardCompatibility(album4);
    }

    [Fact]
    public async Task GetAlbums_AlbumWithNoGenres_ReturnsEmptyGenreArray()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlbumsResponse>();
        Assert.NotNull(result);

        var album5 = result!.Data.FirstOrDefault(a => a.Title == "Test Album 5");
        Assert.NotNull(album5);
        TestHelpers.AssertMultiValuedTags(album5, 1, 0);
        Assert.Empty(album5.Genres);
    }

    [Fact]
    public async Task GetAlbums_PrimaryArtistAppearsFirst()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlbumsResponse>();
        Assert.NotNull(result);

        var album2 = result!.Data.FirstOrDefault(a => a.Title == "Test Album 2");
        Assert.NotNull(album2);
        
        // Primary artist should be first
        Assert.Equal(album2.ArtistId, album2.ArtistIds[0]);
        Assert.Equal(album2.ArtistName, album2.ArtistNames[0]);
    }

    [Fact]
    public async Task GetAlbums_PaginationWorks()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/albums?page=1&limit=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlbumsResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.Data.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.Limit);
        Assert.True(result.Total > 0);
    }

    [Fact]
    public async Task GetAlbumById_ReturnsMultiValuedTagArrays()
    {
        // Arrange
        var albumsResponse = await _client.GetAsync("/api/v2/albums");
        var albumsResult = await albumsResponse.Content.ReadFromJsonAsync<AlbumsResponse>();
        var albumId = albumsResult!.Data.First().Id;

        // Act
        var response = await _client.GetAsync($"/api/v2/albums/{albumId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlbumResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Data);

        var album = result.Data;
        Assert.NotNull(album.ArtistIds);
        Assert.NotNull(album.ArtistNames);
        Assert.NotNull(album.Genres);
        TestHelpers.AssertBackwardCompatibility(album);
    }

    [Fact]
    public async Task GetAlbumById_AlbumWithMultipleArtists_ReturnsAllArtists()
    {
        // Arrange
        var albumsResponse = await _client.GetAsync("/api/v2/albums");
        var albumsResult = await albumsResponse.Content.ReadFromJsonAsync<AlbumsResponse>();
        var album4 = albumsResult!.Data.FirstOrDefault(a => a.Title == "Test Album 4");
        Assert.NotNull(album4);

        // Act
        var response = await _client.GetAsync($"/api/v2/albums/{album4.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlbumResponse>();
        Assert.NotNull(result);
        
        var album = result!.Data;
        TestHelpers.AssertMultiValuedTags(album, 3, 3);
        Assert.Contains("Artist E", album.ArtistNames);
        Assert.Contains("Artist F", album.ArtistNames);
        Assert.Contains("Artist G", album.ArtistNames);
    }

    [Fact]
    public async Task GetAlbumById_Returns404ForNonExistentAlbum()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/albums/nonexistent-id");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _context?.Dispose();
    }

    private class AlbumsResponse
    {
        public List<AlbumDto> Data { get; set; } = new();
        public int Page { get; set; }
        public int Limit { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
    }

    private class AlbumResponse
    {
        public AlbumDto Data { get; set; } = null!;
    }
}
