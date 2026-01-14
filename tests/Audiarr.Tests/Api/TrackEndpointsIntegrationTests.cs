using System.Net;
using System.Net.Http.Json;
using Audiarr.Core.DTOs;
using Audiarr.Data.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Audiarr.Tests.Api;

public class TrackEndpointsIntegrationTests : IClassFixture<AudiarrWebApplicationFactory>, IDisposable
{
    private readonly AudiarrWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly DbContextScope _context;
    private readonly TestDataBuilder _dataBuilder;

    public TrackEndpointsIntegrationTests(AudiarrWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
        _context = factory.GetDbContext();
        _dataBuilder = new TestDataBuilder(_context.Context);
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Clear existing data
        _context.Context.TrackGenres.RemoveRange(_context.Context.TrackGenres);
        _context.Context.TrackArtists.RemoveRange(_context.Context.TrackArtists);
        _context.Context.AlbumGenres.RemoveRange(_context.Context.AlbumGenres);
        _context.Context.AlbumArtists.RemoveRange(_context.Context.AlbumArtists);
        _context.Context.Tracks.RemoveRange(_context.Context.Tracks);
        _context.Context.Albums.RemoveRange(_context.Context.Albums);
        _context.Context.Genres.RemoveRange(_context.Context.Genres);
        _context.Context.Artists.RemoveRange(_context.Context.Artists);
        _context.Context.SaveChanges();

        // Create test data with various scenarios
        // Single artist, single genre
        var album1 = _dataBuilder.CreateAlbum("Album 1", new[] { "Artist A" }, new[] { "Rock" }, 2020);
        var track1 = _dataBuilder.CreateTrack("Track 1", album1, new[] { "Artist A" }, new[] { "Rock" }, 1, 1);
        
        // Multiple artists, single genre
        var album2 = _dataBuilder.CreateAlbum("Album 2", new[] { "Artist B", "Artist C" }, new[] { "Pop" }, 2021);
        var track2 = _dataBuilder.CreateTrack("Track 2", album2, new[] { "Artist B", "Artist C" }, new[] { "Pop" }, 1, 1);
        
        // Single artist, multiple genres
        var album3 = _dataBuilder.CreateAlbum("Album 3", new[] { "Artist D" }, new[] { "Electronic", "House" }, 2022);
        var track3 = _dataBuilder.CreateTrack("Track 3", album3, new[] { "Artist D" }, new[] { "Electronic", "House" }, 1, 1);
        
        // Multiple artists, multiple genres
        var album4 = _dataBuilder.CreateAlbum("Album 4", new[] { "Artist E", "Artist F", "Artist G" }, 
            new[] { "Jazz", "Fusion", "Smooth" }, 2023);
        var track4 = _dataBuilder.CreateTrack("Track 4", album4, 
            new[] { "Artist E", "Artist F", "Artist G" }, 
            new[] { "Jazz", "Fusion", "Smooth" }, 1, 1);
        
        // Single artist, no genre
        var album5 = _dataBuilder.CreateAlbum("Album 5", new[] { "Artist H" }, null, 2024);
        var track5 = _dataBuilder.CreateTrack("Track 5", album5, new[] { "Artist H" }, null, 1, 1);
        
        _dataBuilder.SaveChanges();
    }

    [Fact]
    public async Task GetTracks_ReturnsMultiValuedTagArrays()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TracksResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Count > 0);

        // Check that all tracks have arrays
        foreach (var track in result.Data)
        {
            Assert.NotNull(track.ArtistIds);
            Assert.NotNull(track.ArtistNames);
            Assert.NotNull(track.Genres);
        }
    }

    [Fact]
    public async Task GetTracks_ReturnsBackwardCompatibleFields()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TracksResponse>();
        Assert.NotNull(result);

        // Check backward compatibility
        foreach (var track in result!.Data)
        {
            TestHelpers.AssertBackwardCompatibility(track);
        }
    }

    [Fact]
    public async Task GetTracks_TrackWithSingleArtist_ReturnsCorrectArrays()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TracksResponse>();
        Assert.NotNull(result);

        var track1 = result!.Data.FirstOrDefault(t => t.Title == "Track 1");
        Assert.NotNull(track1);
        TestHelpers.AssertMultiValuedTags(track1, 1, 1);
        TestHelpers.AssertBackwardCompatibility(track1);
    }

    [Fact]
    public async Task GetTracks_TrackWithMultipleArtists_ReturnsCorrectArrays()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TracksResponse>();
        Assert.NotNull(result);

        var track2 = result!.Data.FirstOrDefault(t => t.Title == "Track 2");
        Assert.NotNull(track2);
        TestHelpers.AssertMultiValuedTags(track2, 2, 1);
        TestHelpers.AssertBackwardCompatibility(track2);
        Assert.Equal(2, track2.ArtistIds.Length);
    }

    [Fact]
    public async Task GetTracks_TrackWithMultipleGenres_ReturnsCorrectArrays()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TracksResponse>();
        Assert.NotNull(result);

        var track3 = result!.Data.FirstOrDefault(t => t.Title == "Track 3");
        Assert.NotNull(track3);
        TestHelpers.AssertMultiValuedTags(track3, 1, 2);
        TestHelpers.AssertBackwardCompatibility(track3);
        Assert.Equal(2, track3.Genres.Length);
    }

    [Fact]
    public async Task GetTracks_TrackWithMultipleArtistsAndGenres_ReturnsCorrectArrays()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TracksResponse>();
        Assert.NotNull(result);

        var track4 = result!.Data.FirstOrDefault(t => t.Title == "Track 4");
        Assert.NotNull(track4);
        TestHelpers.AssertMultiValuedTags(track4, 3, 3);
        TestHelpers.AssertBackwardCompatibility(track4);
    }

    [Fact]
    public async Task GetTracks_TrackWithNoGenres_ReturnsEmptyGenreArray()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TracksResponse>();
        Assert.NotNull(result);

        var track5 = result!.Data.FirstOrDefault(t => t.Title == "Track 5");
        Assert.NotNull(track5);
        TestHelpers.AssertMultiValuedTags(track5, 1, 0);
        Assert.Empty(track5.Genres);
    }

    [Fact]
    public async Task GetTracks_PrimaryArtistAppearsFirst()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TracksResponse>();
        Assert.NotNull(result);

        var track2 = result!.Data.FirstOrDefault(t => t.Title == "Track 2");
        Assert.NotNull(track2);
        
        // Primary artist should be first
        Assert.Equal(track2.ArtistId, track2.ArtistIds[0]);
        Assert.Equal(track2.ArtistName, track2.ArtistNames[0]);
    }

    [Fact]
    public async Task GetTracks_PaginationWorks()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/tracks?page=1&limit=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TracksResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.Data.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.Limit);
        Assert.True(result.Total > 0);
    }

    [Fact]
    public async Task GetTrackById_ReturnsMultiValuedTagArrays()
    {
        // Arrange
        var tracksResponse = await _client.GetAsync("/api/v2/tracks");
        var tracksResult = await tracksResponse.Content.ReadFromJsonAsync<TracksResponse>();
        var trackId = tracksResult!.Data.First().Id;

        // Act
        var response = await _client.GetAsync($"/api/v2/tracks/{trackId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var track = await response.Content.ReadFromJsonAsync<TrackDto>();
        Assert.NotNull(track);
        Assert.NotNull(track.ArtistIds);
        Assert.NotNull(track.ArtistNames);
        Assert.NotNull(track.Genres);
        TestHelpers.AssertBackwardCompatibility(track);
    }

    [Fact]
    public async Task GetTrackById_TrackWithMultipleArtists_ReturnsAllArtists()
    {
        // Arrange
        var tracksResponse = await _client.GetAsync("/api/v2/tracks");
        var tracksResult = await tracksResponse.Content.ReadFromJsonAsync<TracksResponse>();
        var track4 = tracksResult!.Data.FirstOrDefault(t => t.Title == "Track 4");
        Assert.NotNull(track4);

        // Act
        var response = await _client.GetAsync($"/api/v2/tracks/{track4.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var track = await response.Content.ReadFromJsonAsync<TrackDto>();
        Assert.NotNull(track);
        
        TestHelpers.AssertMultiValuedTags(track, 3, 3);
        Assert.Contains("Artist E", track.ArtistNames);
        Assert.Contains("Artist F", track.ArtistNames);
        Assert.Contains("Artist G", track.ArtistNames);
    }

    [Fact]
    public async Task GetTrackById_Returns404ForNonExistentTrack()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/tracks/nonexistent-id");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _context?.Dispose();
    }

    private class TracksResponse
    {
        public List<TrackDto> Data { get; set; } = new();
        public int Page { get; set; }
        public int Limit { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
    }
}
