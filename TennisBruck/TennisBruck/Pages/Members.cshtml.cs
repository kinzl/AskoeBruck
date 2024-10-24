using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TennisBruck.Services;
using TennisDb;

namespace TennisBruck.Pages;

public class Members : PageModel
{
    private TennisContext _db;
    public PlayerService PlayerService { get; set; }
    private readonly ILogger<IndexModel> _logger;
    public List<Player> Players { get; set; }

    public Members(TennisContext db, ILogger<IndexModel> logger, PlayerService playerService)
    {
        _db = db;
        _logger = logger;
        PlayerService = playerService;
    }

    public RedirectToPageResult? OnGet()
    {
        if (HttpContext.User.Identities.ToList().First().Name == null) return new RedirectToPageResult(nameof(Login));
        _logger.LogInformation("Id {Name} Signed in", HttpContext.User.Identities.ToList().First().Name);
        Players = _db.Players.ToList();
        return null;
    }

    public IActionResult OnPostDeleteUser(int playerId)
    {
        _logger.LogInformation("OnPostDeleteUser");
        var player = _db.Players.Single(x => x.Id == playerId);
        _db.Players.Remove(player);
        _db.SaveChanges();
        return new RedirectToPageResult(nameof(Members));
    }
}