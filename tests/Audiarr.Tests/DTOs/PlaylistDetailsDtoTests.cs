using Audiarr.Core.DTOs;
using Xunit;

namespace Audiarr.Tests.DTOs;

public class PlaylistDetailsDtoTests
{
    [Fact]
    public void PlaylistDetailsDto_Constructor_InitializesTracksList()
    {
        // Arrange & Act
        var dto = new PlaylistDetailsDto();

        // Assert
        Assert.NotNull(dto.Tracks);
        Assert.Empty(dto.Tracks);
        Assert.IsType<List<PlaylistTrackDto>>(dto.Tracks);
    }

    [Fact]
    public void PlaylistDetailsDto_InheritsAllPlaylistDtoProperties()
    {
        // Arrange
        var dto = new PlaylistDetailsDto
        {
            Id = "playlist123",
            Name = "Test Playlist",
            Description = "Description",
            UserId = "user456",
            Username = "testuser",
            IsPublic = true,
            ImagePath = "/images/playlist.jpg",
            TrackCount = 5,
            TotalDuration = TimeSpan.FromMinutes(20),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            PlayCount = 50
        };

        // Assert - All base properties are present
        Assert.Equal("playlist123", dto.Id);
        Assert.Equal("Test Playlist", dto.Name);
        Assert.Equal("Description", dto.Description);
        Assert.Equal("user456", dto.UserId);
        Assert.Equal("testuser", dto.Username);
        Assert.True(dto.IsPublic);
        Assert.Equal("/images/playlist.jpg", dto.ImagePath);
        Assert.Equal(5, dto.TrackCount);
        Assert.Equal(TimeSpan.FromMinutes(20), dto.TotalDuration);
        Assert.Equal(50, dto.PlayCount);
    }

    [Fact]
    public void PlaylistDetailsDto_CanAddTracksToList()
    {
        // Arrange
        var dto = new PlaylistDetailsDto();
        var track1 = new PlaylistTrackDto
        {
            TrackId = "track1",
            Title = "Song 1",
            ArtistName = "Artist 1",
            Position = 0,
            PositionFloat = 0
        };
        var track2 = new PlaylistTrackDto
        {
            TrackId = "track2",
            Title = "Song 2",
            ArtistName = "Artist 2",
            Position = 1,
            PositionFloat = 1
        };

        // Act
        dto.Tracks.Add(track1);
        dto.Tracks.Add(track2);

        // Assert
        Assert.Equal(2, dto.Tracks.Count);
        Assert.Contains(track1, dto.Tracks);
        Assert.Contains(track2, dto.Tracks);
        Assert.Equal("track1", dto.Tracks[0].TrackId);
        Assert.Equal("track2", dto.Tracks[1].TrackId);
    }

    [Fact]
    public void PlaylistDetailsDto_CanSetTracksListDirectly()
    {
        // Arrange
        var dto = new PlaylistDetailsDto();
        var tracks = new List<PlaylistTrackDto>
        {
            new() { TrackId = "track1", Title = "Song 1" },
            new() { TrackId = "track2", Title = "Song 2" },
            new() { TrackId = "track3", Title = "Song 3" }
        };

        // Act
        dto.Tracks = tracks;

        // Assert
        Assert.Equal(3, dto.Tracks.Count);
        Assert.Same(tracks, dto.Tracks);
    }

    [Fact]
    public void PlaylistDetailsDto_TracksListCanBeEmpty()
    {
        // Arrange & Act
        var dto = new PlaylistDetailsDto
        {
            Id = "playlist123",
            Name = "Empty Playlist",
            TrackCount = 0,
            Tracks = new List<PlaylistTrackDto>()
        };

        // Assert
        Assert.Empty(dto.Tracks);
        Assert.Equal(0, dto.TrackCount);
    }
}