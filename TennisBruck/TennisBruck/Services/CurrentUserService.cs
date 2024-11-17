using System.Security.Claims;
using TennisDb;

namespace TennisBruck.Services;

public class CurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TennisContext _db;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, TennisContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public Player? GetCurrentUser(string? sessionName)
    {
        var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return null;

        return _db.Players.Find(int.Parse(userId));
    }
}