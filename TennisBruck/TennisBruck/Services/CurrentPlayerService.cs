namespace TennisBruck.Services;

public class CurrentPlayerService(IHttpContextAccessor httpContextAccessor, TennisContext db)
{
    private Player? _cachedPlayer;

    public Player? GetCurrentUser()
    {
        if (_cachedPlayer != null) return _cachedPlayer;

        var userId = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        _cachedPlayer = string.IsNullOrEmpty(userId)
            ? null
            : db.Players
                .Include(x => x.IdentityUser)
                .SingleOrDefault(x => x.IdentityUserId == userId);

        return _cachedPlayer;
    }
}