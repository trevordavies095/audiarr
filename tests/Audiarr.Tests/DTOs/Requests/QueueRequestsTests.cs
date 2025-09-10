using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Audiarr.Core.DTOs.Requests;
using Audiarr.Core.Entities;
using Xunit;

namespace Audiarr.Tests.DTOs.Requests;

public class QueueRequestsTests
{
    #region AddToQueueRequest Tests

    [Fact]
    public void AddToQueueRequest_ValidRequest_PassesValidation()
    {
        // Arrange
        var request = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track1", "track2", "track3" },
            Source = "album",
            PlayNext = true
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
    public void AddToQueueRequest_EmptyTrackIds_FailsValidation()
    {
        // Arrange
        var request = new AddToQueueRequest
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
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("At least one track must be provided"));
    }

    [Fact]
    public void AddToQueueRequest_TooManyTracks_FailsValidation()
    {
        // Arrange
        var trackIds = Enumerable.Range(1, 101).Select(i => $"track{i}").ToList();
        var request = new AddToQueueRequest
        {
            TrackIds = trackIds
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("Cannot add more than 100 tracks"));
    }

    [Fact]
    public void AddToQueueRequest_ExactlyHundredTracks_PassesValidation()
    {
        // Arrange
        var trackIds = Enumerable.Range(1, 100).Select(i => $"track{i}").ToList();
        var request = new AddToQueueRequest
        {
            TrackIds = trackIds
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
    public void AddToQueueRequest_DefaultPlayNext_IsFalse()
    {
        // Arrange & Act
        var request = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track1" }
        };

        // Assert
        Assert.False(request.PlayNext);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("album")]
    [InlineData("playlist")]
    [InlineData("This is a valid source string that is exactly 100 characters long to test the maximum limit!!!")]
    public void AddToQueueRequest_ValidSource_PassesValidation(string? source)
    {
        // Arrange
        var request = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track1" },
            Source = source
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
    public void AddToQueueRequest_SourceTooLong_FailsValidation()
    {
        // Arrange
        var request = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track1" },
            Source = new string('x', 101)
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
    }

    #endregion

    #region UpdateQueueRequest Tests

    [Fact]
    public void UpdateQueueRequest_AllFieldsOptional_PassesValidation()
    {
        // Arrange
        var request = new UpdateQueueRequest();

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
        Assert.Null(request.RepeatMode);
        Assert.Null(request.IsShuffled);
        Assert.Null(request.CurrentIndex);
    }

    [Fact]
    public void UpdateQueueRequest_ValidValues_PassesValidation()
    {
        // Arrange
        var request = new UpdateQueueRequest
        {
            RepeatMode = RepeatMode.All,
            IsShuffled = true,
            CurrentIndex = 5
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
    public void UpdateQueueRequest_NegativeIndex_FailsValidation()
    {
        // Arrange
        var request = new UpdateQueueRequest
        {
            CurrentIndex = -1
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("Current index must be non-negative"));
    }

    [Theory]
    [InlineData(RepeatMode.None)]
    [InlineData(RepeatMode.One)]
    [InlineData(RepeatMode.All)]
    public void UpdateQueueRequest_AllRepeatModes_AreValid(RepeatMode mode)
    {
        // Arrange
        var request = new UpdateQueueRequest
        {
            RepeatMode = mode
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    #endregion

    #region ReorderQueueRequest Tests

    [Fact]
    public void ReorderQueueRequest_ValidRequest_PassesValidation()
    {
        // Arrange
        var request = new ReorderQueueRequest
        {
            TrackId = "track123",
            NewIndex = 5
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
    public void ReorderQueueRequest_NegativeIndex_FailsValidation()
    {
        // Arrange
        var request = new ReorderQueueRequest
        {
            TrackId = "track123",
            NewIndex = -1
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("New index must be non-negative"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(999)]
    [InlineData(int.MaxValue)]
    public void ReorderQueueRequest_ValidIndices_PassValidation(int index)
    {
        // Arrange
        var request = new ReorderQueueRequest
        {
            TrackId = "track123",
            NewIndex = index
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    #endregion

    #region ClearQueueRequest Tests

    [Fact]
    public void ClearQueueRequest_DefaultKeepCurrentTrack_IsFalse()
    {
        // Arrange & Act
        var request = new ClearQueueRequest();

        // Assert
        Assert.False(request.KeepCurrentTrack);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClearQueueRequest_CanSetKeepCurrentTrack(bool keepCurrent)
    {
        // Arrange & Act
        var request = new ClearQueueRequest
        {
            KeepCurrentTrack = keepCurrent
        };

        // Assert
        Assert.Equal(keepCurrent, request.KeepCurrentTrack);
    }

    #endregion

    #region ReplaceQueueRequest Tests

    [Fact]
    public void ReplaceQueueRequest_ValidRequest_PassesValidation()
    {
        // Arrange
        var request = new ReplaceQueueRequest
        {
            TrackIds = new List<string> { "track1", "track2", "track3" },
            StartIndex = 0,
            Source = "playlist"
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
    public void ReplaceQueueRequest_EmptyTrackIds_FailsValidation()
    {
        // Arrange
        var request = new ReplaceQueueRequest
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
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("At least one track must be provided"));
    }

    [Fact]
    public void ReplaceQueueRequest_TooManyTracks_FailsValidation()
    {
        // Arrange
        var trackIds = Enumerable.Range(1, 1001).Select(i => $"track{i}").ToList();
        var request = new ReplaceQueueRequest
        {
            TrackIds = trackIds
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("Queue cannot exceed 1000 tracks"));
    }

    [Fact]
    public void ReplaceQueueRequest_ExactlyThousandTracks_PassesValidation()
    {
        // Arrange
        var trackIds = Enumerable.Range(1, 1000).Select(i => $"track{i}").ToList();
        var request = new ReplaceQueueRequest
        {
            TrackIds = trackIds
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
    public void ReplaceQueueRequest_DefaultStartIndex_IsZero()
    {
        // Arrange & Act
        var request = new ReplaceQueueRequest
        {
            TrackIds = new List<string> { "track1" }
        };

        // Assert
        Assert.Equal(0, request.StartIndex);
    }

    [Fact]
    public void ReplaceQueueRequest_NegativeStartIndex_FailsValidation()
    {
        // Arrange
        var request = new ReplaceQueueRequest
        {
            TrackIds = new List<string> { "track1" },
            StartIndex = -1
        };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("Start index must be non-negative"));
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void QueueRequests_SerializeAndDeserialize_Correctly()
    {
        // Arrange
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "track1", "track2" },
            Source = "album",
            PlayNext = true
        };

        // Act
        var json = JsonSerializer.Serialize(addRequest);
        var deserialized = JsonSerializer.Deserialize<AddToQueueRequest>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(addRequest.TrackIds.Count, deserialized.TrackIds.Count);
        Assert.Equal(addRequest.Source, deserialized.Source);
        Assert.Equal(addRequest.PlayNext, deserialized.PlayNext);
    }

    [Fact]
    public void UpdateQueueRequest_EnumSerialization_WorksCorrectly()
    {
        // Arrange
        var request = new UpdateQueueRequest
        {
            RepeatMode = RepeatMode.All,
            IsShuffled = true,
            CurrentIndex = 10
        };

        // Act
        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<UpdateQueueRequest>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(RepeatMode.All, deserialized.RepeatMode);
        Assert.True(deserialized.IsShuffled);
        Assert.Equal(10, deserialized.CurrentIndex);
    }

    #endregion
}