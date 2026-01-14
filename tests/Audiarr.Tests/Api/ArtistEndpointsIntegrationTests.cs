using System.Net;
using System.Net.Http.Json;
using Audiarr.Core.DTOs;
using Audiarr.Data.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Audiarr.Tests.Api;

public class ArtistEndpointsIntegrationTests : IClassFixture<AudiarrWebApplicationFactory>, IDisposable
{
    private readonly AudiarrWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly DbContextScope _context;
    private readonly TestDataBuilder _dataBuilder;

    public ArtistEndpointsIntegrationTests(AudiarrWebApplicationFactory factory)
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

        // Create test data:
        // Artist A is primary artist on Album 1
        var album1 = _dataBuilder.CreateAlbum("Album 1", new[] { "Artist A" }, new[] { "Rock" }, 2020);
        var track1 = _dataBuilder.CreateTrack("Track 1", album1, new[] { "Artist A" }, new[] { "Rock" }, 1, 1);

        // Artist B is primary, Artist C is contributing on Album 2
        var album2 = _dataBuilder.CreateAlbum("Album 2", new[] { "Artist B", "Artist C" }, new[] { "Pop" }, 2021);
        var track2 = _dataBuilder.CreateTrack("Track 1", album2, new[] { "Artist B", "Artist C" }, new[] { "Pop" }, 1, 1);
        var track3 = _dataBuilder.CreateTrack("Track 2", album2, new[] { "Artist B" }, new[] { "Pop" }, 2, 1);

        // Artist C is primary on Album 3, Artist B is contributing
        var album3 = _dataBuilder.CreateAlbum("Album 3", new[] { "Artist C", "Artist B" }, new[] { "Jazz" }, 2022);
        var track4 = _dataBuilder.CreateTrack("Track 1", album3, new[] { "Artist C", "Artist B" }, new[] { "Jazz" }, 1, 1);

        // Artist D is only contributing (not primary) on Album 4
        var album4 = _dataBuilder.CreateAlbum("Album 4", new[] { "Artist E", "Artist D" }, new[] { "Electronic" }, 2023);
        var track5 = _dataBuilder.CreateTrack("Track 1", album4, new[] { "Artist E", "Artist D" }, new[] { "Electronic" }, 1, 1);

        _dataBuilder.SaveChanges();
    }

    [Fact]
    public async Task GetArtistAlbums_ReturnsAlbumsWhereArtistIsPrimary()
    {
        // Arrange
        var artistA = await _context.Artists.FirstAsync(a => a.Name == "Artist A");

        // Act
        var response = await _client.GetAsync($"/api/v2/artists/{artistA.Id}/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ArtistAlbumsResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal("Album 1", result.Data[0].Title);
    }

    [Fact]
    public async Task GetArtistAlbums_ReturnsAlbumsWhereArtistIsContributing()
    {
        // Arrange
        var artistC = await _context.Artists.FirstAsync(a => a.Name == "Artist C");

        // Act
        var response = await _client.GetAsync($"/api/v2/artists/{artistC.Id}/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ArtistAlbumsResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        
        // Should return both Album 2 (where C is contributing) and Album 3 (where C is primary)
        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, a => a.Title == "Album 2");
        Assert.Contains(result.Data, a => a.Title == "Album 3");
    }

    [Fact]
    public async Task GetArtistAlbums_ReturnsAlbumsWhereArtistIsOnlyContributing()
    {
        // Arrange
        var artistD = await _context.Artists.FirstAsync(a => a.Name == "Artist D");

        // Act
        var response = await _client.GetAsync($"/api/v2/artists/{artistD.Id}/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ArtistAlbumsResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        
        // Should return Album 4 even though D is not the primary artist
        Assert.Single(result.Data);
        Assert.Equal("Album 4", result.Data[0].Title);
    }

    [Fact]
    public async Task GetArtistAlbums_NoDuplicateAlbums()
    {
        // Arrange
        var artistB = await _context.Artists.FirstAsync(a => a.Name == "Artist B");

        // Act
        var response = await _client.GetAsync($"/api/v2/artists/{artistB.Id}/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ArtistAlbumsResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        
        // Artist B appears in both Album 2 and Album 3, but should only appear once per album
        var albumTitles = result.Data.Select(a => a.Title).ToList();
        Assert.Equal(albumTitles.Count, albumTitles.Distinct().Count());
    }

    [Fact]
    public async Task GetArtistAlbums_ReturnsMultiValuedTagArrays()
    {
        // Arrange
        var artistC = await _context.Artists.FirstAsync(a => a.Name == "Artist C");

        // Act
        var response = await _client.GetAsync($"/api/v2/artists/{artistC.Id}/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ArtistAlbumsResponse>();
        Assert.NotNull(result);

        foreach (var album in result!.Data)
        {
            Assert.NotNull(album.ArtistIds);
            Assert.NotNull(album.ArtistNames);
            Assert.NotNull(album.Genres);
            TestHelpers.AssertBackwardCompatibility(album);
        }
    }

    [Fact]
    public async Task GetArtistTracks_ReturnsTracksWhereArtistIsPrimary()
    {
        // Arrange
        var artistA = await _context.Artists.FirstAsync(a => a.Name == "Artist A");

        // Act
        var response = await _client.GetAsync($"/api/v2/artists/{artistA.Id}/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ArtistTracksResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal("Track 1", result.Data[0].Title);
    }

    [Fact]
    public async Task GetArtistTracks_ReturnsTracksWhereArtistIsContributing()
    {
        // Arrange
        var artistC = await _context.Artists.FirstAsync(a => a.Name == "Artist C");

        // Act
        var response = await _client.GetAsync($"/api/v2/artists/{artistC.Id}/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ArtistTracksResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        
        // Should return Track 1 from Album 2 (where C is contributing) and Track 1 from Album 3 (where C is primary)
        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, t => t.Title == "Track 1" && t.AlbumTitle == "Album 2");
        Assert.Contains(result.Data, t => t.Title == "Track 1" && t.AlbumTitle == "Album 3");
    }

    [Fact]
    public async Task GetArtistTracks_ReturnsTracksWhereArtistIsOnlyContributing()
    {
        // Arrange
        var artistD = await _context.Artists.FirstAsync(a => a.Name == "Artist D");

        // Act
        var response = await _client.GetAsync($"/api/v2/artists/{artistD.Id}/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ArtistTracksResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        
        // Should return Track 1 from Album 4 even though D is not the primary artist
        Assert.Single(result.Data);
        Assert.Equal("Track 1", result.Data[0].Title);
        Assert.Equal("Album 4", result.Data[0].AlbumTitle);
    }

    [Fact]
    public async Task GetArtistTracks_NoDuplicateTracks()
    {
        // Arrange
        var artistB = await _context.Artists.FirstAsync(a => a.Name == "Artist B");

        // Act
        var response = await _client.GetAsync($"/api/v2/artists/{artistB.Id}/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ArtistTracksResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        
        // Artist B appears in multiple tracks, but each track should only appear once
        var trackIds = result.Data.Select(t => t.Id).ToList();
        Assert.Equal(trackIds.Count, trackIds.Distinct().Count());
    }

    [Fact]
    public async Task GetArtistTracks_ReturnsMultiValuedTagArrays()
    {
        // Arrange
        var artistC = await _context.Artists.FirstAsync(a => a.Name == "Artist C");

        // Act
        var response = await _client.GetAsync($"/api/v2/artists/{artistC.Id}/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ArtistTracksResponse>();
        Assert.NotNull(result);

        foreach (var track in result!.Data)
        {
            Assert.NotNull(track.ArtistIds);
            Assert.NotNull(track.ArtistNames);
            Assert.NotNull(track.Genres);
            TestHelpers.AssertBackwardCompatibility(track);
        }
    }

    [Fact]
    public async Task GetArtistAlbums_Returns404ForNonExistentArtist()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/artists/nonexistent-id/albums");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetArtistTracks_Returns404ForNonExistentArtist()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/artists/nonexistent-id/tracks");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _context?.Dispose();
    }

    private class ArtistAlbumsResponse
    {
        public List<AlbumDto> Data { get; set; } = new();
    }

    private class ArtistTracksResponse
    {
        public List<TrackDto> Data { get; set; } = new();
    }
}
