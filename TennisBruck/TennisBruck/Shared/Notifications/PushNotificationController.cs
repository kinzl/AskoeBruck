namespace TennisBruck.Shared.Notifications;

public class PushSubscriptionRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
}

public class PushUnsubscribeRequest
{
    public string Endpoint { get; set; } = string.Empty;
}

[ApiController]
[Route("api/push")]
public class PushNotificationController(
    WebPushNotificationService pushService,
    CurrentPlayerService currentPlayerService)
    : ControllerBase
{
    [HttpGet("public-key")]
    public IActionResult GetPublicKey()
    {
        var key = pushService.GetVapidPublicKey();
        return Ok(new { publicKey = key });
    }

    [Authorize]
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionRequest request)
    {
        var player = currentPlayerService.GetCurrentUser();
        if (player == null) return Unauthorized();

        var success = await pushService.SubscribeAsync(
            player.Id,
            request.Endpoint,
            request.P256dh,
            request.Auth
        );

        if (!success) return BadRequest(new { message = "Ungültige Abonnement-Daten" });

        return Ok(new { success = true });
    }

    [Authorize]
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeRequest request)
    {
        var success = await pushService.UnsubscribeAsync(request.Endpoint);
        return Ok(new { success });
    }

    [Authorize]
    [HttpPost("test")]
    public async Task<IActionResult> SendTestNotification()
    {
        var player = currentPlayerService.GetCurrentUser();
        if (player == null) return Unauthorized();

        var success = await pushService.SendTestNotificationAsync(player.Id);
        return Ok(new { success });
    }
}
