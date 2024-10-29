using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TennisBruck.Services;
using TennisDb;

namespace TennisBruck.Pages;

public class CourtGrieskirchen : PageModel
{
    private readonly PlayerService _playerService;
    private TennisContext _db;
    private PlanService _planService;
    private ILogger<CourtGrieskirchen> _logger;
    public Player LoggedInPlayer { get; set; }
    public List<Court> Courts { get; set; }

    public CourtGrieskirchen(PlayerService playerService, TennisContext db, PlanService planService,
        ILogger<CourtGrieskirchen> logger)
    {
        _playerService = playerService;
        _db = db;
        _planService = planService;
        _logger = logger;
    }

    public IActionResult OnGet()
    {
        if (HttpContext.User.Identities.ToList().First().Name == null) return new RedirectToPageResult(nameof(Login));
        LoggedInPlayer = _playerService.GetPlayer(HttpContext.User.Identities.ToList().First().Name)!;
        Courts = _db.Court
            .Include(x => x.PlayerCourtGrieskirchens)
            .ThenInclude(x => x.Player)
            .OrderBy(x => x.MatchDay)
            .ToList();

        return Page();
    }

    public IActionResult OnPostChangePlayingState()
    {
        var player = _db.Players.Single(x => x.Id == int.Parse(HttpContext.User.Identities.ToList().First().Name!));
        player.IsPlayingGrieskirchen = !player.IsPlayingGrieskirchen;
        _db.SaveChanges();

        return new RedirectToPageResult(nameof(CourtGrieskirchen));
    }

    public IActionResult OnPostGeneratePlan(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Starting plan generation");
        _planService.GeneratePlanGrieskirchen(startDate, endDate);
        _logger.LogInformation("Plan generation complete");
        return new RedirectToPageResult(nameof(CourtGrieskirchen));
    }
}