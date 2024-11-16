using System.Text.Json;
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

    public async Task<IActionResult> OnPostSwapPlayers([FromBody] JsonElement data)
    {
        if (!data.TryGetProperty("player1Id", out var player1IdProp) ||
            !data.TryGetProperty("player2Id", out var player2IdProp) ||
            !data.TryGetProperty("court1Id", out var court1IdProp) ||
            !data.TryGetProperty("court2Id", out var court2IdProp))
        {
            return BadRequest("Invalid data.");
        }

        if (!int.TryParse(player1IdProp.GetString(), out int player1Id) ||
            !int.TryParse(player2IdProp.GetString(), out int player2Id) ||
            !int.TryParse(court1IdProp.GetString(), out int court1Id) ||
            !int.TryParse(court2IdProp.GetString(), out int court2Id))
        {
            return BadRequest("Invalid data format.");
        }

        // Find the first court and player association
        var playerCourt1 = await _db.PlayerCourtGrieskirchen
            .Include(pc => pc.Player)
            .Include(pc => pc.Court)
            .FirstOrDefaultAsync(pc => pc.Player.Id == player1Id && pc.Court.Id == court1Id);

        // Find the second court and player association
        var playerCourt2 = await _db.PlayerCourtGrieskirchen
            .Include(pc => pc.Player)
            .Include(pc => pc.Court)
            .FirstOrDefaultAsync(pc => pc.Player.Id == player2Id && pc.Court.Id == court2Id);

        if (playerCourt1 == null || playerCourt2 == null)
        {
            return BadRequest("One or both players not found in specified courts.");
        }

        // Remove both entries from the database
        _db.PlayerCourtGrieskirchen.Remove(playerCourt1);
        _db.PlayerCourtGrieskirchen.Remove(playerCourt2);
        await _db.SaveChangesAsync();

        // Re-add entries with swapped court and player assignments
        _db.PlayerCourtGrieskirchen.Add(new PlayerCourtGrieskirchen
        {
            Player = _db.Players.Single(x => x.Id == player2Id),
            Court = _db.Court.Single(x => x.Id == court1Id)
        });

        _db.PlayerCourtGrieskirchen.Add(new PlayerCourtGrieskirchen
        {
            Player = _db.Players.Single(x => x.Id == player1Id),
            Court = _db.Court.Single(x => x.Id == court2Id)
        });

        await _db.SaveChangesAsync();
        OnGet();
        return new JsonResult(new { success = true });
    }

    public IActionResult OnPostGeneratePlan(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Starting plan generation");
        _planService.GeneratePlanGrieskirchen(startDate, endDate);
        _logger.LogInformation("Plan generation complete");
        return new RedirectToPageResult(nameof(CourtGrieskirchen));
    }
}