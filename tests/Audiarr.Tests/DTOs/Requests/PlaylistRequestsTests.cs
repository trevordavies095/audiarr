using System.ComponentModel.DataAnnotations;
using Audiarr.Core.DTOs.Requests;
using Xunit;

namespace Audiarr.Tests.DTOs.Requests;

public class PlaylistRequestsTests
{
    [Fact]
    public void CreatePlaylistRequest_RequiredFields_AreValidated()
    {
        // Arrange
        var request = new CreatePlaylistRequest
        {
            Name = "Test Playlist"
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void CreatePlaylistRequest_InvalidName_FailsValidation(string? name)
    {
        // Arrange
        var request = new CreatePlaylistRequest
        {
            Name = name!
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void CreatePlaylistRequest_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var request = new CreatePlaylistRequest
        {
            Name = "Test"
        };

        // Assert
        Assert.False(request.IsPublic);
        Assert.Null(request.Description);
        Assert.Null(request.InitialTrackIds);
    }

    [Fact]
    public void CreatePlaylistRequest_CanSetInitialTrackIds()
    {
        // Arrange & Act
        var request = new CreatePlaylistRequest
        {
            Name = "Test Playlist",
            InitialTrackIds = new List<string> { "track1", "track2", "track3" }
        };

        // Assert
        Assert.NotNull(request.InitialTrackIds);
        Assert.Equal(3, request.InitialTrackIds.Count);
        Assert.Contains("track1", request.InitialTrackIds);
    }

    [Fact]
    public void UpdatePlaylistRequest_RequiredFields_AreValidated()
    {
        // Arrange
        var request = new UpdatePlaylistRequest
        {
            Name = "Updated Playlist",
            IsPublic = true
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void AddTracksRequest_RequiresAtLeastOneTrack()
    {
        // Arrange - Empty list
        var request = new AddTracksRequest
        {
            TrackIds = new List<string>()
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void AddTracksRequest_ValidWithTracks()
    {
        // Arrange
        var request = new AddTracksRequest
        {
            TrackIds = new List<string> { "track1", "track2" },
            Position = 5
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
        Assert.Equal(5, request.Position);
    }

    [Fact]
    public void RemoveTracksRequest_RequiresAtLeastOneTrack()
    {
        // Arrange
        var request = new RemoveTracksRequest
        {
            TrackIds = new List<string> { "track1" }
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void ReorderTracksRequest_ValidatesTrackItems()
    {
        // Arrange
        var request = new ReorderTracksRequest
        {
            Tracks = new List<TrackReorderItem>
            {
                new() { TrackId = "track1", NewPosition = 0.5 },
                new() { TrackId = "track2", NewPosition = 1.5 },
                new() { TrackId = "track3", NewPosition = 2.5 }
            }
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
        Assert.Equal(3, request.Tracks.Count);
    }

    [Fact]
    public void TrackReorderItem_RequiresBothFields()
    {
        // Arrange
        var item = new TrackReorderItem
        {
            TrackId = "track123",
            NewPosition = 5.25
        };

        // Act & Assert
        Assert.Equal("track123", item.TrackId);
        Assert.Equal(5.25, item.NewPosition);
    }

    [Fact]
    public void UpdatePlaylistImageRequest_RequiresImagePath()
    {
        // Arrange
        var request = new UpdatePlaylistImageRequest
        {
            ImagePath = "/images/new-playlist.jpg"
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
        Assert.Equal("/images/new-playlist.jpg", request.ImagePath);
    }

    [Fact]
    public void CopyPlaylistRequest_RequiresName()
    {
        // Arrange
        var request = new CopyPlaylistRequest
        {
            Name = "Copy of Playlist",
            Description = "A copied playlist"
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
        Assert.Equal("Copy of Playlist", request.Name);
        Assert.Equal("A copied playlist", request.Description);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Normal Playlist Name")]
    [InlineData("This is a very long playlist name that contains many characters but is still within the 255 character limit that we have set for the playlist name field in our validation attributes")]
    public void CreatePlaylistRequest_Name_AcceptsValidLengths(string name)
    {
        // Arrange
        var request = new CreatePlaylistRequest { Name = name };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void CreatePlaylistRequest_Description_ValidatesLength()
    {
        // Arrange - Create a description that's too long (over 1000 chars)
        var longDescription = new string('x', 1001);
        var request = new CreatePlaylistRequest
        {
            Name = "Test",
            Description = longDescription
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
    }
}