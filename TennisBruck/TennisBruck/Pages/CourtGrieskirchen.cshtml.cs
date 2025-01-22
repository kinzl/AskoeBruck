using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TennisBruck.Services;
using TennisDb;

namespace TennisBruck.Pages;

[Authorize]
public class CourtGrieskirchen : PageModel
{
    private readonly CurrentPlayerService _currentPlayerService;
    private TennisContext _db;
    private PlanService _planService;
    private ILogger<CourtGrieskirchen> _logger;
    public Player LoggedInPlayer { get; set; }
    [BindProperty] public List<Court> Courts { get; set; }

    public CourtGrieskirchen(CurrentPlayerService currentPlayerService, TennisContext db, PlanService planService,
        ILogger<CourtGrieskirchen> logger)
    {
        _currentPlayerService = currentPlayerService;
        _db = db;
        _planService = planService;
        _logger = logger;
    }

    public IActionResult OnGet()
    {
        LoggedInPlayer = _currentPlayerService.GetCurrentUser(HttpContext.User.Identities.ToList().First().Name)!;
        Courts = _db.Court
            .Include(x => x.PlayerCourtGrieskirchens)
            .ThenInclude(x => x.Player)
            .OrderBy(x => x.MatchDay)
            .ToList();

        return Page();
    }

    public IActionResult OnPostGeneratePlan(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Starting plan generation");
        _planService.GeneratePlanGrieskirchen(startDate, endDate);
        _logger.LogInformation("Plan generation complete");
        return new RedirectToPageResult(nameof(CourtGrieskirchen));
    }

    public IActionResult OnPostChangePlayingState()
    {
        var player = _db.Players.Single(x => x.Id == int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value));
        player.IsPlayingGrieskirchen = !player.IsPlayingGrieskirchen;
        _db.SaveChanges();

        return new RedirectToPageResult(nameof(CourtGrieskirchen));
    }

    public IActionResult OnPostBack()
    {
        return new RedirectToPageResult(nameof(Index));
    }
}