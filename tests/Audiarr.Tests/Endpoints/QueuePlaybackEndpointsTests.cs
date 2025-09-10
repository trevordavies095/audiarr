using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Audiarr.Core.DTOs;
using Audiarr.Core.DTOs.Requests;
using Audiarr.Core.Entities;
using Audiarr.Data.Context;
using Xunit;

namespace Audiarr.Tests.Endpoints;

public class QueuePlaybackEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public QueuePlaybackEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AudiarrContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<AudiarrContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                });

                // Ensure the database is created and seeded
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AudiarrContext>();
                db.Database.EnsureCreated();
                SeedTestData(db);
            });
        });

        _client = _factory.CreateClient();
    }

    private void SeedTestData(AudiarrContext context)
    {
        // Add test user
        var user = new User
        {
            Id = "test-user",
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("testpass"),
            Role = "user"
        };
        context.Users.Add(user);

        // Add test tracks
        var artist = new Artist
        {
            Id = "artist-1",
            Name = "Test Artist"
        };
        context.Artists.Add(artist);

        var album = new Album
        {
            Id = "album-1",
            Title = "Test Album",
            ArtistId = artist.Id
        };
        context.Albums.Add(album);

        for (int i = 1; i <= 5; i++)
        {
            var track = new Track
            {
                Id = $"track-{i}",
                Title = $"Test Track {i}",
                FilePath = $"/music/track{i}.mp3",
                DurationMs = 180000,
                ArtistId = artist.Id,
                AlbumId = album.Id,
                TrackNumber = i
            };
            context.Tracks.Add(track);
        }

        context.SaveChanges();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var loginRequest = new { username = "testuser", password = "testpass" };
        var response = await _client.PostAsJsonAsync("/api/v2/auth/login", loginRequest);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<JsonElement>(content);
        return authResponse.GetProperty("accessToken").GetString()!;
    }

    private async Task<QueueStateDto> SetupQueueWithTracksAsync(string token)
    {
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var replaceRequest = new ReplaceQueueRequest
        {
            TrackIds = new List<string> { "track-1", "track-2", "track-3", "track-4", "track-5" },
            StartIndex = 0
        };

        var response = await _client.PostAsJsonAsync("/api/v2/queue/replace", replaceRequest);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<QueueStateDto>() 
            ?? throw new InvalidOperationException("Failed to setup queue");
    }

    #region PUT /api/v2/queue/settings Tests

    [Fact]
    public async Task UpdateSettings_Should_Update_RepeatMode_And_Shuffle()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        await SetupQueueWithTracksAsync(token);

        var updateRequest = new UpdateQueueRequest
        {
            RepeatMode = RepeatMode.All,
            IsShuffled = true
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/v2/queue/settings", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<QueueStateDto>();
        Assert.NotNull(result);
        Assert.Equal(RepeatMode.All, result.RepeatMode);
        Assert.True(result.IsShuffled);
    }

    [Fact]
    public async Task UpdateSettings_Should_Return_401_When_Not_Authenticated()
    {
        // Arrange
        var updateRequest = new UpdateQueueRequest
        {
            RepeatMode = RepeatMode.One
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/v2/queue/settings", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region POST /api/v2/queue/next Tests

    [Fact]
    public async Task NextTrack_Should_Move_To_Next_Track()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        var initialQueue = await SetupQueueWithTracksAsync(token);
        Assert.Equal(0, initialQueue.CurrentIndex);
        Assert.Equal("track-1", initialQueue.CurrentTrackId);

        // Act
        var response = await _client.PostAsync("/api/v2/queue/next", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<QueueStateDto>();
        Assert.NotNull(result);
        Assert.Equal(1, result.CurrentIndex);
        Assert.Equal("track-2", result.CurrentTrackId);
    }

    [Fact]
    public async Task NextTrack_Should_Return_404_When_Queue_Empty()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync("/api/v2/queue/next", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Queue is empty", content);
    }

    [Fact]
    public async Task NextTrack_Should_Return_401_When_Not_Authenticated()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PostAsync("/api/v2/queue/next", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NextTrack_Should_Loop_When_RepeatAll()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        await SetupQueueWithTracksAsync(token);

        // Set to last track with RepeatMode.All
        await _client.PutAsJsonAsync("/api/v2/queue/settings", new UpdateQueueRequest 
        { 
            CurrentIndex = 4,
            RepeatMode = RepeatMode.All 
        });

        // Act
        var response = await _client.PostAsync("/api/v2/queue/next", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<QueueStateDto>();
        Assert.NotNull(result);
        Assert.Equal(0, result.CurrentIndex); // Looped to first
        Assert.Equal("track-1", result.CurrentTrackId);
    }

    #endregion

    #region POST /api/v2/queue/previous Tests

    [Fact]
    public async Task PreviousTrack_Should_Move_To_Previous_Track()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        await SetupQueueWithTracksAsync(token);
        
        // Move to track 3
        await _client.PutAsJsonAsync("/api/v2/queue/settings", new UpdateQueueRequest { CurrentIndex = 2 });

        // Act
        var response = await _client.PostAsync("/api/v2/queue/previous", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<QueueStateDto>();
        Assert.NotNull(result);
        Assert.Equal(1, result.CurrentIndex);
        Assert.Equal("track-2", result.CurrentTrackId);
    }

    [Fact]
    public async Task PreviousTrack_Should_Return_404_When_Queue_Empty()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync("/api/v2/queue/previous", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Queue is empty", content);
    }

    [Fact]
    public async Task PreviousTrack_Should_Return_401_When_Not_Authenticated()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PostAsync("/api/v2/queue/previous", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PreviousTrack_Should_Loop_When_RepeatAll()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        await SetupQueueWithTracksAsync(token);

        // Set to first track with RepeatMode.All
        await _client.PutAsJsonAsync("/api/v2/queue/settings", new UpdateQueueRequest 
        { 
            CurrentIndex = 0,
            RepeatMode = RepeatMode.All 
        });

        // Act
        var response = await _client.PostAsync("/api/v2/queue/previous", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<QueueStateDto>();
        Assert.NotNull(result);
        Assert.Equal(4, result.CurrentIndex); // Looped to last
        Assert.Equal("track-5", result.CurrentTrackId);
    }

    #endregion

    #region PUT /api/v2/queue/position/{index} Tests

    [Fact]
    public async Task JumpToPosition_Should_Jump_To_Valid_Index()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        await SetupQueueWithTracksAsync(token);

        // Act
        var response = await _client.PutAsync("/api/v2/queue/position/3", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<QueueStateDto>();
        Assert.NotNull(result);
        Assert.Equal(3, result.CurrentIndex);
        Assert.Equal("track-4", result.CurrentTrackId);
    }

    [Fact]
    public async Task JumpToPosition_Should_Return_400_When_Index_Out_Of_Range()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        await SetupQueueWithTracksAsync(token);

        // Act
        var response = await _client.PutAsync("/api/v2/queue/position/10", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Index 10 is out of range", content);
    }

    [Fact]
    public async Task JumpToPosition_Should_Return_400_When_Index_Negative()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        await SetupQueueWithTracksAsync(token);

        // Act
        var response = await _client.PutAsync("/api/v2/queue/position/-1", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task JumpToPosition_Should_Return_404_When_Queue_Empty()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PutAsync("/api/v2/queue/position/0", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Queue is empty", content);
    }

    [Fact]
    public async Task JumpToPosition_Should_Return_401_When_Not_Authenticated()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PutAsync("/api/v2/queue/position/0", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}