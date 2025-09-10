using Audiarr.Core.DTOs;
using Xunit;

namespace Audiarr.Tests.DTOs;

public class PlaylistDtoTests
{
    [Fact]
    public void PlaylistDto_Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var dto = new PlaylistDto();

        // Assert
        Assert.Equal(string.Empty, dto.Id);
        Assert.Equal(string.Empty, dto.Name);
        Assert.Null(dto.Description);
        Assert.Equal(string.Empty, dto.UserId);
        Assert.Equal(string.Empty, dto.Username);
        Assert.False(dto.IsPublic);
        Assert.Null(dto.ImagePath);
        Assert.Equal(0, dto.TrackCount);
        Assert.Null(dto.TotalDuration);
        Assert.Equal(default(DateTime), dto.CreatedAt);
        Assert.Equal(default(DateTime), dto.UpdatedAt);
        Assert.Equal(default(DateTime), dto.LastModified);
        Assert.Equal(0, dto.PlayCount);
    }

    [Fact]
    public void PlaylistDto_CanSetAndGetAllProperties()
    {
        // Arrange
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var testDuration = TimeSpan.FromMinutes(45.5);

        var dto = new PlaylistDto();

        // Act
        dto.Id = "playlist123";
        dto.Name = "Test Playlist";
        dto.Description = "A test playlist";
        dto.UserId = "user456";
        dto.Username = "testuser";
        dto.IsPublic = true;
        dto.ImagePath = "/images/playlist.jpg";
        dto.TrackCount = 25;
        dto.TotalDuration = testDuration;
        dto.CreatedAt = testDate;
        dto.UpdatedAt = testDate.AddHours(1);
        dto.LastModified = testDate.AddHours(2);
        dto.PlayCount = 100;

        // Assert
        Assert.Equal("playlist123", dto.Id);
        Assert.Equal("Test Playlist", dto.Name);
        Assert.Equal("A test playlist", dto.Description);
        Assert.Equal("user456", dto.UserId);
        Assert.Equal("testuser", dto.Username);
        Assert.True(dto.IsPublic);
        Assert.Equal("/images/playlist.jpg", dto.ImagePath);
        Assert.Equal(25, dto.TrackCount);
        Assert.Equal(testDuration, dto.TotalDuration);
        Assert.Equal(testDate, dto.CreatedAt);
        Assert.Equal(testDate.AddHours(1), dto.UpdatedAt);
        Assert.Equal(testDate.AddHours(2), dto.LastModified);
        Assert.Equal(100, dto.PlayCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Short description")]
    [InlineData("This is a very long description that contains many characters to test the field capacity")]
    public void PlaylistDto_Description_AcceptsVariousValues(string? description)
    {
        // Arrange & Act
        var dto = new PlaylistDto { Description = description };

        // Assert
        Assert.Equal(description, dto.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(10000)]
    public void PlaylistDto_TrackCount_AcceptsValidValues(int trackCount)
    {
        // Arrange & Act
        var dto = new PlaylistDto { TrackCount = trackCount };

        // Assert
        Assert.Equal(trackCount, dto.TrackCount);
    }
}