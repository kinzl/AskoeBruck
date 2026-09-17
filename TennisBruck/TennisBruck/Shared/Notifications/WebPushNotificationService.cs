using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TennisDb;
using WebPush;

namespace TennisBruck.Shared.Notifications;

public class VapidConfig
{
    public string Subject { get; set; } = "mailto:admin@tennisbruck.at";
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
}

public class WebPushNotificationService
{
    private readonly TennisContext _db;
    private readonly ILogger<WebPushNotificationService> _logger;
    private readonly VapidDetails _vapidDetails;
    private readonly WebPushClient _client;
    private static VapidDetails? _cachedVapidDetails;
    private static readonly object _vapidLock = new();

    public WebPushNotificationService(
        TennisContext db,
        IConfiguration config,
        ILogger<WebPushNotificationService> logger)
    {
        _db = db;
        _logger = logger;
        _client = new WebPushClient();

        var subject = config["Vapid:Subject"] ?? "mailto:admin@tennisbruck.at";
        var publicKey = config["Vapid:PublicKey"];
        var privateKey = config["Vapid:PrivateKey"];

        if (!string.IsNullOrWhiteSpace(publicKey) && !string.IsNullOrWhiteSpace(privateKey))
        {
            _vapidDetails = new VapidDetails(subject, publicKey, privateKey);
        }
        else
        {
            if (_cachedVapidDetails == null)
            {
                lock (_vapidLock)
                {
                    if (_cachedVapidDetails == null)
                    {
                        var generatedKeys = VapidHelper.GenerateVapidKeys();
                        _cachedVapidDetails = new VapidDetails(subject, generatedKeys.PublicKey, generatedKeys.PrivateKey);
                        _logger.LogInformation("Generated ephemeral in-memory VAPID keys for Web Push.");
                    }
                }
            }
            _vapidDetails = _cachedVapidDetails;
        }
    }

    public string GetVapidPublicKey() => _vapidDetails.PublicKey;

    public async Task<bool> SubscribeAsync(int playerId, string endpoint, string p256dh, string auth)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(p256dh) || string.IsNullOrWhiteSpace(auth))
        {
            return false;
        }

        var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (existing != null)
        {
            existing.PlayerId = playerId;
            existing.P256dh = p256dh;
            existing.Auth = auth;
            existing.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.PushSubscriptions.Add(new PushSubscriptionEntity
            {
                PlayerId = playerId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Registered push subscription for player {PlayerId}", playerId);
        return true;
    }

    public async Task<bool> UnsubscribeAsync(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return false;

        var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (existing != null)
        {
            _db.PushSubscriptions.Remove(existing);
            await _db.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public async Task<int> SendNotificationAsync(int playerId, string title, string message, string? url = null)
    {
        var subscriptions = await _db.PushSubscriptions
            .Where(s => s.PlayerId == playerId)
            .ToListAsync();

        if (!subscriptions.Any())
        {
            _logger.LogDebug("No push subscriptions found for player {PlayerId}", playerId);
            return 0;
        }

        var payload = JsonSerializer.Serialize(new
        {
            title,
            body = message,
            url = url ?? "/",
            icon = "/images/app-icon-192.png",
            badge = "/images/app-icon-192.png"
        });

        int sentCount = 0;
        var expiredSubscriptions = new List<PushSubscriptionEntity>();

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await _client.SendNotificationAsync(pushSub, payload, _vapidDetails);
                sentCount++;
            }
            catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone || ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Subscription expired or gone for player {PlayerId}. Removing endpoint: {Endpoint}", playerId, sub.Endpoint);
                expiredSubscriptions.Add(sub);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send web push notification to endpoint {Endpoint} for player {PlayerId}", sub.Endpoint, playerId);
            }
        }

        if (expiredSubscriptions.Any())
        {
            _db.PushSubscriptions.RemoveRange(expiredSubscriptions);
            await _db.SaveChangesAsync();
        }

        return sentCount;
    }

    public async Task<bool> SendTestNotificationAsync(int playerId)
    {
        var count = await SendNotificationAsync(
            playerId,
            "🎾 TennisBruck Benachrichtigungen",
            "Super! Web Push-Benachrichtigungen sind auf diesem Gerät erfolgreich aktiviert.",
            "/Settings"
        );

        return count > 0;
    }
}
