using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TennisBruck.Shared.Notifications;
using TennisDb;
using Xunit;

namespace TennisBruck.Tests.Shared;

public class WebPushNotificationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TennisContext _db;
    private readonly IConfiguration _config;

    public WebPushNotificationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TennisContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TennisContext(options);
        _db.Database.EnsureCreated();

        _config = new ConfigurationBuilder().Build();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void GetVapidPublicKey_GeneratesValidP256PublicKey()
    {
        // Arrange
        var service = new WebPushNotificationService(_db, _config, NullLogger<WebPushNotificationService>.Instance);

        // Act
        var key = service.GetVapidPublicKey();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(key));
        Assert.True(key.Length >= 80, "P-256 base64url public key should be at least 80 characters");
    }

    [Fact]
    public async Task SubscribeAsync_NewSubscription_PersistsToDatabase()
    {
        // Arrange
        var player = new Player { Id = 1, Firstname = "Max", Lastname = "Mustermann" };
        _db.Players.Add(player);
        await _db.SaveChangesAsync();

        var service = new WebPushNotificationService(_db, _config, NullLogger<WebPushNotificationService>.Instance);

        // Act
        var success = await service.SubscribeAsync(
            playerId: 1,
            endpoint: "https://fcm.googleapis.com/fcm/send/test-endpoint-1",
            p256dh: "test-p256dh-key-12345",
            auth: "test-auth-secret-12345"
        );

        // Assert
        Assert.True(success);
        var sub = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.PlayerId == 1);
        Assert.NotNull(sub);
        Assert.Equal("https://fcm.googleapis.com/fcm/send/test-endpoint-1", sub.Endpoint);
        Assert.Equal("test-p256dh-key-12345", sub.P256dh);
        Assert.Equal("test-auth-secret-12345", sub.Auth);
    }

    [Fact]
    public async Task SubscribeAsync_ExistingEndpoint_UpdatesWithoutDuplicate()
    {
        // Arrange
        var player1 = new Player { Id = 1, Firstname = "Max", Lastname = "Mustermann" };
        var player2 = new Player { Id = 2, Firstname = "Anna", Lastname = "Musterfrau" };
        _db.Players.AddRange(player1, player2);
        await _db.SaveChangesAsync();

        var service = new WebPushNotificationService(_db, _config, NullLogger<WebPushNotificationService>.Instance);

        await service.SubscribeAsync(1, "https://fcm.googleapis.com/test", "old-key", "old-auth");

        // Act - Re-subscribe same endpoint for player 2 with updated keys
        var success = await service.SubscribeAsync(2, "https://fcm.googleapis.com/test", "new-key", "new-auth");

        // Assert
        Assert.True(success);
        var subs = await _db.PushSubscriptions.Where(s => s.Endpoint == "https://fcm.googleapis.com/test").ToListAsync();
        Assert.Single(subs);
        Assert.Equal(2, subs[0].PlayerId);
        Assert.Equal("new-key", subs[0].P256dh);
        Assert.Equal("new-auth", subs[0].Auth);
    }

    [Fact]
    public async Task UnsubscribeAsync_ExistingEndpoint_RemovesSubscription()
    {
        // Arrange
        var player = new Player { Id = 1, Firstname = "Max", Lastname = "Mustermann" };
        _db.Players.Add(player);
        await _db.SaveChangesAsync();

        var service = new WebPushNotificationService(_db, _config, NullLogger<WebPushNotificationService>.Instance);
        await service.SubscribeAsync(1, "https://fcm.googleapis.com/remove-me", "key", "auth");

        // Act
        var unsubSuccess = await service.UnsubscribeAsync("https://fcm.googleapis.com/remove-me");

        // Assert
        Assert.True(unsubSuccess);
        var sub = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == "https://fcm.googleapis.com/remove-me");
        Assert.Null(sub);
    }
}
