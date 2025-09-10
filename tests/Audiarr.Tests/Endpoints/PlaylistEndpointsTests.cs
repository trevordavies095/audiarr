using System.Net;
using System.Net.Http.Json;
using Audiarr.Core.DTOs.Requests;
using Xunit;

namespace Audiarr.Tests.Endpoints;

public class PlaylistEndpointsTests
{
    [Fact]
    public void CreatePlaylistRequest_Validation_Works()
    {
        // Arrange
        var validRequest = new CreatePlaylistRequest
        {
            Name = "Test Playlist",
            Description = "A test playlist",
            IsPublic = false
        };

        // Act & Assert
        Assert.NotNull(validRequest.Name);
        Assert.Equal("Test Playlist", validRequest.Name);
        Assert.Equal("A test playlist", validRequest.Description);
        Assert.False(validRequest.IsPublic);
    }

    [Fact]
    public void UpdatePlaylistRequest_Validation_Works()
    {
        // Arrange
        var validRequest = new UpdatePlaylistRequest
        {
            Name = "Updated Playlist",
            Description = "Updated description",
            IsPublic = true
        };

        // Act & Assert
        Assert.NotNull(validRequest.Name);
        Assert.Equal("Updated Playlist", validRequest.Name);
        Assert.Equal("Updated description", validRequest.Description);
        Assert.True(validRequest.IsPublic);
    }

    [Fact]
    public void AddTracksRequest_Validation_Works()
    {
        // Arrange
        var validRequest = new AddTracksRequest
        {
            TrackIds = new List<string> { "track1", "track2", "track3" },
            Position = 5
        };

        // Act & Assert
        Assert.NotNull(validRequest.TrackIds);
        Assert.Equal(3, validRequest.TrackIds.Count);
        Assert.Equal(5, validRequest.Position);
    }

    [Fact]
    public void RemoveTracksRequest_Validation_Works()
    {
        // Arrange
        var validRequest = new RemoveTracksRequest
        {
            TrackIds = new List<string> { "track1", "track2" }
        };

        // Act & Assert
        Assert.NotNull(validRequest.TrackIds);
        Assert.Equal(2, validRequest.TrackIds.Count);
    }

    [Fact]
    public void ReorderTracksRequest_Validation_Works()
    {
        // Arrange
        var validRequest = new ReorderTracksRequest
        {
            Tracks = new List<TrackReorderItem>
            {
                new() { TrackId = "track1", NewPosition = 0.5m },
                new() { TrackId = "track2", NewPosition = 1.5m }
            }
        };

        // Act & Assert
        Assert.NotNull(validRequest.Tracks);
        Assert.Equal(2, validRequest.Tracks.Count);
        Assert.Equal("track1", validRequest.Tracks[0].TrackId);
        Assert.Equal(0.5m, validRequest.Tracks[0].NewPosition);
    }

    [Fact]
    public void CreatePlaylistRequest_WithInitialTracks_Works()
    {
        // Arrange
        var trackIds = new List<string> { "track1", "track2", "track3", "track4", "track5" };
        var request = new CreatePlaylistRequest
        {
            Name = "Playlist with Initial Tracks",
            Description = "Testing initial track addition",
            IsPublic = true,
            InitialTrackIds = trackIds
        };

        // Act & Assert
        Assert.NotNull(request.InitialTrackIds);
        Assert.Equal(5, request.InitialTrackIds.Count);
        Assert.Contains("track3", request.InitialTrackIds);
    }

    [Fact]
    public void UpdatePlaylistImageRequest_Validation_Works()
    {
        // Arrange
        var request = new UpdatePlaylistImageRequest
        {
            ImagePath = "/images/playlist-cover.jpg"
        };

        // Act & Assert
        Assert.NotNull(request.ImagePath);
        Assert.Equal("/images/playlist-cover.jpg", request.ImagePath);
    }

    [Fact]
    public void CopyPlaylistRequest_Validation_Works()
    {
        // Arrange
        var request = new CopyPlaylistRequest
        {
            Name = "Copy of My Playlist",
            Description = "A copied playlist"
        };

        // Act & Assert
        Assert.NotNull(request.Name);
        Assert.Equal("Copy of My Playlist", request.Name);
        Assert.Equal("A copied playlist", request.Description);
    }
}