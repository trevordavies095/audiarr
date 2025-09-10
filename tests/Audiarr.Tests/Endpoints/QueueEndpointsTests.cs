using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Audiarr.Core.DTOs;
using Audiarr.Core.DTOs.Requests;
using Audiarr.Core.Entities;
using Audiarr.Data.Context;

namespace Audiarr.Tests.Endpoints;

public class QueueEndpointsTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string? _accessToken;
    private readonly string _testUserId = "test-user-queue";

    public QueueEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AudiarrContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<AudiarrContext>(options =>
                {
                    options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
                });

                // Build service provider
                var sp = services.BuildServiceProvider();

                // Create scope and seed test data
                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<AudiarrContext>();

                    db.Database.EnsureCreated();
                    SeedTestData(db);
                }
            });
        });

        _client = _factory.CreateClient();
    }

    private void SeedTestData(AudiarrContext context)
    {
        // Add test user
        var user = new User
        {
            Id = _testUserId,
            Username = "queuetestuser",
            Email = "queuetest@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = "user",
            IsActive = true
        };
        context.Users.Add(user);

        // Add test artist
        var artist = new Artist
        {
            Id = "artist-test-1",
            Name = "Test Artist"
        };
        context.Artists.Add(artist);

        // Add test album
        var album = new Album
        {
            Id = "album-test-1",
            Title = "Test Album",
            ArtistId = artist.Id
        };
        context.Albums.Add(album);

        // Add test tracks
        for (int i = 1; i <= 10; i++)
        {
            var track = new Track
            {
                Id = $"test-track-{i}",
                Title = $"Test Track {i}",
                FilePath = $"/music/test/track{i}.mp3",
                DurationMs = 180000, // 3 minutes
                ArtistId = artist.Id,
                AlbumId = album.Id,
                TrackNumber = i
            };
            context.Tracks.Add(track);
        }

        context.SaveChanges();
    }

    private async Task AuthenticateAsync()
    {
        if (_accessToken != null) return;

        var loginRequest = new LoginRequest(
            Username: "queuetestuser",
            Password: "password123"
        );

        var response = await _client.PostAsJsonAsync("/api/v2/auth/login", loginRequest);
        response.EnsureSuccessStatusCode();

        var loginResponse = await response.Content.ReadAsStringAsync();
        var loginData = JsonSerializer.Deserialize<JsonElement>(loginResponse);
        _accessToken = loginData.GetProperty("accessToken").GetString();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    #region GET /api/v2/queue Tests

    [Fact]
    public async Task GetQueue_Should_Return_Unauthorized_Without_Token()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/queue");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetQueue_Should_AutoCreate_And_Return_Empty_Queue()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.GetAsync("/api/v2/queue");

        // Assert
        response.EnsureSuccessStatusCode();
        var queue = await response.Content.ReadFromJsonAsync<QueueStateDto>();

        Assert.NotNull(queue);
        Assert.Equal(_testUserId, queue.UserId);
        Assert.Empty(queue.TrackIds);
        Assert.Equal(0, queue.CurrentIndex);
        Assert.Null(queue.CurrentTrackId);
        Assert.Equal(RepeatMode.None, queue.RepeatMode);
        Assert.False(queue.IsShuffled);
        Assert.Equal(1, queue.Version);
    }

    [Fact]
    public async Task GetQueue_Should_Return_Existing_Queue_State()
    {
        // Arrange
        await AuthenticateAsync();

        // First add some tracks
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "test-track-1", "test-track-2", "test-track-3" },
            Source = "test"
        };

        await _client.PostAsJsonAsync("/api/v2/queue/tracks", addRequest);

        // Act
        var response = await _client.GetAsync("/api/v2/queue");

        // Assert
        response.EnsureSuccessStatusCode();
        var queue = await response.Content.ReadFromJsonAsync<QueueStateDto>();

        Assert.NotNull(queue);
        Assert.Equal(3, queue.TrackIds.Count);
        Assert.Equal("test", queue.QueueSource);
    }

    #endregion

    #region POST /api/v2/queue/tracks Tests

    [Fact]
    public async Task AddTracks_Should_Add_Tracks_To_Queue()
    {
        // Arrange
        await AuthenticateAsync();
        var request = new AddToQueueRequest
        {
            TrackIds = new List<string> { "test-track-1", "test-track-2" },
            Source = "album"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v2/queue/tracks", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var queue = await response.Content.ReadFromJsonAsync<QueueStateDto>();

        Assert.NotNull(queue);
        Assert.Equal(2, queue.TrackIds.Count);
        Assert.Equal("test-track-1", queue.CurrentTrackId);
        Assert.Equal("album", queue.QueueSource);
    }

    [Fact]
    public async Task AddTracks_Should_Return_BadRequest_For_NonExistent_Tracks()
    {
        // Arrange
        await AuthenticateAsync();
        var request = new AddToQueueRequest
        {
            TrackIds = new List<string> { "test-track-1", "non-existent-track" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v2/queue/tracks", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("Tracks not found", problemDetails?.Detail);
    }

    [Fact]
    public async Task AddTracks_With_PlayNext_Should_Insert_After_Current()
    {
        // Arrange
        await AuthenticateAsync();

        // First add some tracks
        var initialRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "test-track-1", "test-track-2", "test-track-3" }
        };
        await _client.PostAsJsonAsync("/api/v2/queue/tracks", initialRequest);

        // Now add tracks with PlayNext
        var request = new AddToQueueRequest
        {
            TrackIds = new List<string> { "test-track-4", "test-track-5" },
            PlayNext = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v2/queue/tracks", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var queue = await response.Content.ReadFromJsonAsync<QueueStateDto>();

        Assert.NotNull(queue);
        Assert.Equal(5, queue.TrackIds.Count);
        Assert.Equal("test-track-1", queue.TrackIds[0]); // Current
        Assert.Equal("test-track-4", queue.TrackIds[1]); // Inserted after current
        Assert.Equal("test-track-5", queue.TrackIds[2]);
    }

    #endregion

    #region DELETE /api/v2/queue/tracks/{index} Tests

    [Fact]
    public async Task RemoveTrack_Should_Remove_Track_At_Index()
    {
        // Arrange
        await AuthenticateAsync();

        // Add tracks first
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "test-track-1", "test-track-2", "test-track-3" }
        };
        await _client.PostAsJsonAsync("/api/v2/queue/tracks", addRequest);

        // Act - remove track at index 1
        var response = await _client.DeleteAsync("/api/v2/queue/tracks/1");

        // Assert
        response.EnsureSuccessStatusCode();
        var queue = await response.Content.ReadFromJsonAsync<QueueStateDto>();

        Assert.NotNull(queue);
        Assert.Equal(2, queue.TrackIds.Count);
        Assert.Equal("test-track-1", queue.TrackIds[0]);
        Assert.Equal("test-track-3", queue.TrackIds[1]);
    }

    [Fact]
    public async Task RemoveTrack_Should_Return_BadRequest_For_Invalid_Index()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.DeleteAsync("/api/v2/queue/tracks/0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("Index is out of range", problemDetails?.Detail);
    }

    #endregion

    #region DELETE /api/v2/queue/clear Tests

    [Fact]
    public async Task ClearQueue_Should_Clear_All_Tracks()
    {
        // Arrange
        await AuthenticateAsync();

        // Add tracks first
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "test-track-1", "test-track-2", "test-track-3" }
        };
        await _client.PostAsJsonAsync("/api/v2/queue/tracks", addRequest);

        // Act
        var response = await _client.DeleteAsync("/api/v2/queue/clear?keepCurrentTrack=false");

        // Assert
        response.EnsureSuccessStatusCode();
        var queue = await response.Content.ReadFromJsonAsync<QueueStateDto>();

        Assert.NotNull(queue);
        Assert.Empty(queue.TrackIds);
        Assert.Null(queue.CurrentTrackId);
    }

    [Fact]
    public async Task ClearQueue_Should_Keep_Current_Track_When_Requested()
    {
        // Arrange
        await AuthenticateAsync();

        // Add tracks first
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "test-track-1", "test-track-2", "test-track-3" }
        };
        await _client.PostAsJsonAsync("/api/v2/queue/tracks", addRequest);

        // Update current index to track-2
        var updateRequest = new UpdateQueueRequest { CurrentIndex = 1 };
        await _client.PutAsJsonAsync("/api/v2/queue", updateRequest);

        // Act
        var response = await _client.DeleteAsync("/api/v2/queue/clear?keepCurrentTrack=true");

        // Assert
        response.EnsureSuccessStatusCode();
        var queue = await response.Content.ReadFromJsonAsync<QueueStateDto>();

        Assert.NotNull(queue);
        Assert.Single(queue.TrackIds);
        Assert.Equal("test-track-2", queue.CurrentTrackId);
    }

    #endregion

    #region PUT /api/v2/queue/reorder Tests

    [Fact]
    public async Task ReorderQueue_Should_Move_Track_To_New_Position()
    {
        // Arrange
        await AuthenticateAsync();

        // Add tracks first
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "test-track-1", "test-track-2", "test-track-3", "test-track-4" }
        };
        await _client.PostAsJsonAsync("/api/v2/queue/tracks", addRequest);

        var reorderRequest = new ReorderQueueRequest
        {
            TrackId = "test-track-2",
            NewIndex = 3
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/v2/queue/reorder", reorderRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var queue = await response.Content.ReadFromJsonAsync<QueueStateDto>();

        Assert.NotNull(queue);
        Assert.Equal(4, queue.TrackIds.Count);
        Assert.Equal("test-track-1", queue.TrackIds[0]);
        Assert.Equal("test-track-3", queue.TrackIds[1]);
        Assert.Equal("test-track-4", queue.TrackIds[2]);
        Assert.Equal("test-track-2", queue.TrackIds[3]);
    }

    [Fact]
    public async Task ReorderQueue_Should_Return_BadRequest_For_Invalid_Track()
    {
        // Arrange
        await AuthenticateAsync();

        var reorderRequest = new ReorderQueueRequest
        {
            TrackId = "non-existent",
            NewIndex = 0
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/v2/queue/reorder", reorderRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("not found in queue", problemDetails?.Detail);
    }

    #endregion

    #region PUT /api/v2/queue Tests

    [Fact]
    public async Task UpdateQueueSettings_Should_Update_RepeatMode()
    {
        // Arrange
        await AuthenticateAsync();

        var updateRequest = new UpdateQueueRequest
        {
            RepeatMode = RepeatMode.All
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/v2/queue", updateRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var queue = await response.Content.ReadFromJsonAsync<QueueStateDto>();

        Assert.NotNull(queue);
        Assert.Equal(RepeatMode.All, queue.RepeatMode);
    }

    [Fact]
    public async Task UpdateQueueSettings_Should_Enable_Shuffle()
    {
        // Arrange
        await AuthenticateAsync();

        // Add tracks first
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "test-track-1", "test-track-2", "test-track-3", "test-track-4", "test-track-5" }
        };
        await _client.PostAsJsonAsync("/api/v2/queue/tracks", addRequest);

        var updateRequest = new UpdateQueueRequest
        {
            IsShuffled = true
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/v2/queue", updateRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var queue = await response.Content.ReadFromJsonAsync<QueueStateDto>();

        Assert.NotNull(queue);
        Assert.True(queue.IsShuffled);
        Assert.Equal(5, queue.TrackIds.Count);
        // All tracks should still be present
        Assert.Contains("test-track-1", queue.TrackIds);
        Assert.Contains("test-track-2", queue.TrackIds);
        Assert.Contains("test-track-3", queue.TrackIds);
        Assert.Contains("test-track-4", queue.TrackIds);
        Assert.Contains("test-track-5", queue.TrackIds);
    }

    [Fact]
    public async Task UpdateQueueSettings_Should_Update_CurrentIndex()
    {
        // Arrange
        await AuthenticateAsync();

        // Add tracks first
        var addRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "test-track-1", "test-track-2", "test-track-3" }
        };
        await _client.PostAsJsonAsync("/api/v2/queue/tracks", addRequest);

        var updateRequest = new UpdateQueueRequest
        {
            CurrentIndex = 2
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/v2/queue", updateRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var queue = await response.Content.ReadFromJsonAsync<QueueStateDto>();

        Assert.NotNull(queue);
        Assert.Equal(2, queue.CurrentIndex);
        Assert.Equal("test-track-3", queue.CurrentTrackId);
    }

    #endregion

    #region POST /api/v2/queue/replace Tests

    [Fact]
    public async Task ReplaceQueue_Should_Replace_Entire_Queue()
    {
        // Arrange
        await AuthenticateAsync();

        // Add initial tracks
        var initialRequest = new AddToQueueRequest
        {
            TrackIds = new List<string> { "test-track-1", "test-track-2" }
        };
        await _client.PostAsJsonAsync("/api/v2/queue/tracks", initialRequest);

        var replaceRequest = new ReplaceQueueRequest
        {
            TrackIds = new List<string> { "test-track-3", "test-track-4", "test-track-5" },
            Source = "playlist",
            StartIndex = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v2/queue/replace", replaceRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var queue = await response.Content.ReadFromJsonAsync<QueueStateDto>();

        Assert.NotNull(queue);
        Assert.Equal(3, queue.TrackIds.Count);
        Assert.Equal("test-track-3", queue.TrackIds[0]);
        Assert.Equal("test-track-4", queue.TrackIds[1]);
        Assert.Equal("test-track-5", queue.TrackIds[2]);
        Assert.Equal(1, queue.CurrentIndex);
        Assert.Equal("test-track-4", queue.CurrentTrackId);
        Assert.Equal("playlist", queue.QueueSource);
    }

    #endregion

    public void Dispose()
    {
        _client?.Dispose();
    }
}