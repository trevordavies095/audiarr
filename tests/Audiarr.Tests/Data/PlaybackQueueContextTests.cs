using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Audiarr.Data.Context;
using Audiarr.Core.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Audiarr.Tests.Data;

public class PlaybackQueueContextTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AudiarrContext> _options;

    public PlaybackQueueContextTests()
    {
        // Create an in-memory SQLite database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Enable foreign keys in SQLite
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        _options = new DbContextOptionsBuilder<AudiarrContext>()
            .UseSqlite(_connection)
            .Options;

        // Create the schema
        using var context = new AudiarrContext(_options);
        context.Database.EnsureCreated();
    }

    #region Configuration Tests

    [Fact]
    public void PlaybackQueue_Entity_Should_Be_Configured()
    {
        using var context = new AudiarrContext(_options);
        var entityType = context.Model.FindEntityType(typeof(PlaybackQueue));

        Assert.NotNull(entityType);
        Assert.Equal("PlaybackQueues", entityType.GetTableName());
    }

    [Fact]
    public void PlaybackQueue_Should_Have_Correct_Indexes()
    {
        using var context = new AudiarrContext(_options);
        var entityType = context.Model.FindEntityType(typeof(PlaybackQueue));

        Assert.NotNull(entityType);

        var indexes = entityType.GetIndexes().ToList();

        // Verify UserId unique index exists (one queue per user)
        var userIdIndex = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == "UserId"));
        Assert.NotNull(userIdIndex);
        Assert.True(userIdIndex.IsUnique);

        // Verify LastActivity index exists (for cleanup)
        Assert.Contains(indexes, i => i.Properties.Any(p => p.Name == "LastActivity"));

        // Verify CurrentTrackId index exists
        Assert.Contains(indexes, i => i.Properties.Any(p => p.Name == "CurrentTrackId"));
    }

    [Fact]
    public void PlaybackQueue_Should_Have_Correct_Property_Defaults()
    {
        using var context = new AudiarrContext(_options);
        var entityType = context.Model.FindEntityType(typeof(PlaybackQueue));

        Assert.NotNull(entityType);

        // Check QueueStateJson default value
        var queueStateJson = entityType.FindProperty("QueueStateJson");
        Assert.NotNull(queueStateJson);
        Assert.Equal("{}", queueStateJson.GetDefaultValue());

        // Check RepeatMode default value
        var repeatMode = entityType.FindProperty("RepeatMode");
        Assert.NotNull(repeatMode);
        Assert.Equal(RepeatMode.None, repeatMode.GetDefaultValue());

        // Check IsShuffled default value
        var isShuffled = entityType.FindProperty("IsShuffled");
        Assert.NotNull(isShuffled);
        Assert.Equal(false, isShuffled.GetDefaultValue());

        // Check CurrentIndex default value
        var currentIndex = entityType.FindProperty("CurrentIndex");
        Assert.NotNull(currentIndex);
        Assert.Equal(0, currentIndex.GetDefaultValue());

        // Check Version default value
        var version = entityType.FindProperty("Version");
        Assert.NotNull(version);
        Assert.Equal(1, version.GetDefaultValue());
    }

    [Fact]
    public void PlaybackQueue_Should_Have_QueueState_Property_Ignored()
    {
        using var context = new AudiarrContext(_options);
        var entityType = context.Model.FindEntityType(typeof(PlaybackQueue));

        Assert.NotNull(entityType);

        // QueueState should be ignored (not mapped to database)
        var queueStateProperty = entityType.FindProperty("QueueState");
        Assert.Null(queueStateProperty);
    }

    #endregion

    #region Relationship Tests

    [Fact]
    public void PlaybackQueue_Should_Have_OneToOne_Relationship_With_User()
    {
        using var context = new AudiarrContext(_options);
        var entityType = context.Model.FindEntityType(typeof(PlaybackQueue));

        Assert.NotNull(entityType);

        var userNavigation = entityType.FindNavigation("User");
        Assert.NotNull(userNavigation);
        Assert.False(userNavigation.IsCollection);

        var userForeignKey = entityType.GetForeignKeys()
            .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == "UserId"));
        Assert.NotNull(userForeignKey);
        Assert.Equal(DeleteBehavior.Cascade, userForeignKey.DeleteBehavior);
    }

    [Fact]
    public void PlaybackQueue_Should_Have_Optional_Relationship_With_Track()
    {
        using var context = new AudiarrContext(_options);
        var entityType = context.Model.FindEntityType(typeof(PlaybackQueue));

        Assert.NotNull(entityType);

        var trackNavigation = entityType.FindNavigation("CurrentTrack");
        Assert.NotNull(trackNavigation);
        Assert.False(trackNavigation.IsCollection);

        var trackForeignKey = entityType.GetForeignKeys()
            .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == "CurrentTrackId"));
        Assert.NotNull(trackForeignKey);
        Assert.Equal(DeleteBehavior.SetNull, trackForeignKey.DeleteBehavior);
    }

    #endregion

    #region Unique Constraint Tests

    [Fact]
    public async Task Should_Enforce_One_Queue_Per_User_Constraint()
    {
        using var context = new AudiarrContext(_options);

        // Create a user
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Create first queue for user
        var queue1 = new PlaybackQueue
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id
        };
        context.PlaybackQueues.Add(queue1);
        await context.SaveChangesAsync();

        // Clear change tracker to ensure fresh state
        context.ChangeTracker.Clear();

        // Try to create second queue for same user
        var queue2 = new PlaybackQueue
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id
        };
        context.PlaybackQueues.Add(queue2);

        // Should throw exception due to unique constraint
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("UNIQUE", exception.InnerException?.Message ?? "");
    }

    [Fact]
    public async Task Should_Allow_Different_Users_To_Have_Queues()
    {
        using var context = new AudiarrContext(_options);

        // Create two users
        var user1 = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "user1",
            Email = "user1@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        var user2 = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "user2",
            Email = "user2@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.AddRange(user1, user2);
        await context.SaveChangesAsync();

        // Create queue for each user
        var queue1 = new PlaybackQueue
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user1.Id
        };
        var queue2 = new PlaybackQueue
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user2.Id
        };
        context.PlaybackQueues.AddRange(queue1, queue2);

        // Should succeed
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.PlaybackQueues.CountAsync());
    }

    #endregion

    #region Cascade Delete Tests

    [Fact]
    public async Task Should_Cascade_Delete_Queue_When_User_Is_Deleted()
    {
        using var context = new AudiarrContext(_options);

        // Create a user with a queue
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "deleteuser",
            Email = "delete@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var queue = new PlaybackQueue
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            QueueStateJson = "{\"trackIds\":[\"track1\",\"track2\"]}"
        };
        context.PlaybackQueues.Add(queue);
        await context.SaveChangesAsync();

        // Verify queue exists
        Assert.Equal(1, await context.PlaybackQueues.CountAsync());

        // Delete the user
        context.Users.Remove(user);
        await context.SaveChangesAsync();

        // Queue should be deleted
        Assert.Equal(0, await context.PlaybackQueues.CountAsync());
    }

    #endregion

    #region JSON Storage Tests

    [Fact]
    public async Task Should_Store_And_Retrieve_JSON_Queue_State()
    {
        using var context = new AudiarrContext(_options);

        // Create a user
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "jsonuser",
            Email = "json@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Create queue with JSON state (CurrentTrackId set to null since we don't have actual tracks)
        var jsonState = "{\"trackIds\":[\"track1\",\"track2\",\"track3\"],\"originalTrackIds\":[\"track1\",\"track2\",\"track3\"]}";
        var queue = new PlaybackQueue
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            QueueStateJson = jsonState,
            CurrentIndex = 1,
            CurrentTrackId = null, // No actual track in DB
            RepeatMode = RepeatMode.All,
            IsShuffled = true
        };
        context.PlaybackQueues.Add(queue);
        await context.SaveChangesAsync();

        // Clear tracking
        context.ChangeTracker.Clear();

        // Retrieve and verify
        var retrievedQueue = await context.PlaybackQueues
            .FirstOrDefaultAsync(q => q.Id == queue.Id);

        Assert.NotNull(retrievedQueue);
        Assert.Equal(jsonState, retrievedQueue.QueueStateJson);
        Assert.Equal(1, retrievedQueue.CurrentIndex);
        Assert.Null(retrievedQueue.CurrentTrackId);
        Assert.Equal(RepeatMode.All, retrievedQueue.RepeatMode);
        Assert.True(retrievedQueue.IsShuffled);
    }

    [Fact]
    public async Task Should_Use_Default_Empty_JSON_When_Not_Specified()
    {
        using var context = new AudiarrContext(_options);

        // Create a user
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "defaultuser",
            Email = "default@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Create queue without specifying JSON
        var queue = new PlaybackQueue
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id
        };
        context.PlaybackQueues.Add(queue);
        await context.SaveChangesAsync();

        // Clear tracking
        context.ChangeTracker.Clear();

        // Retrieve and verify default
        var retrievedQueue = await context.PlaybackQueues
            .FirstOrDefaultAsync(q => q.Id == queue.Id);

        Assert.NotNull(retrievedQueue);
        Assert.Equal("{}", retrievedQueue.QueueStateJson);
    }

    #endregion

    #region Timestamp Tests

    [Fact]
    public async Task Should_Set_Timestamps_On_Create_And_Update()
    {
        using var context = new AudiarrContext(_options);

        // Create a user
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "timestampuser",
            Email = "timestamp@example.com",
            PasswordHash = "hash",
            Role = "user"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var beforeCreate = DateTime.UtcNow;

        // Create queue
        var queue = new PlaybackQueue
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id
        };
        context.PlaybackQueues.Add(queue);
        await context.SaveChangesAsync();

        var afterCreate = DateTime.UtcNow;

        // Verify creation timestamps
        Assert.InRange(queue.CreatedAt, beforeCreate.AddSeconds(-1), afterCreate);
        Assert.InRange(queue.UpdatedAt, beforeCreate.AddSeconds(-1), afterCreate);
        Assert.InRange(queue.LastActivity, beforeCreate.AddSeconds(-1), afterCreate);

        // Wait a bit and update
        await Task.Delay(100);
        var beforeUpdate = DateTime.UtcNow;

        queue.CurrentIndex = 5;
        await context.SaveChangesAsync();

        var afterUpdate = DateTime.UtcNow;

        // Verify update timestamp changed
        Assert.InRange(queue.UpdatedAt, beforeUpdate.AddSeconds(-1), afterUpdate);
        Assert.True(queue.UpdatedAt > queue.CreatedAt);
    }

    #endregion

    public void Dispose()
    {
        _connection?.Dispose();
    }
}