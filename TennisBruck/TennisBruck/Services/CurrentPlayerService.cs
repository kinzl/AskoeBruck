namespace TennisBruck.Services;

public class CurrentPlayerService(IHttpContextAccessor httpContextAccessor, TennisContext db)
{
    public Player? GetCurrentUser(string? sessionName)
    {
        var userId = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId == null ? null : db.Players.Find(int.Parse(userId));
    }
}