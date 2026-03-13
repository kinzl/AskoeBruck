namespace TennisBruck.Services;

public class CurrentPlayerService(IHttpContextAccessor httpContextAccessor, TennisContext db)
{
    public Player? GetCurrentUser()
    {
        var userId = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(userId)
            ? null
            : db.Players
                .Include(x => x.IdentityUser)
                .SingleOrDefault(x => x.IdentityUserId == userId);
    }
}