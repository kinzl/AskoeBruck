namespace TennisBruck.Controller;

[Authorize]
[Route("[controller]/[action]")]
public class SwapPlayerController : ControllerBase
{
    private TennisContext _db;
    private readonly CurrentPlayerService _currentPlayerService;

    public SwapPlayerController(TennisContext db, CurrentPlayerService currentPlayerService)
    {
        _db = db;
        _currentPlayerService = currentPlayerService;
    }

    [HttpPost]
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

        var currentUser = _currentPlayerService.GetCurrentUser();
        if (currentUser == null) return Unauthorized("Invalid user");

        var court1 = await _db.Court.FindAsync(court1Id);
        if (court1 == null) return BadRequest("Court not found");

        bool isRegistered = await _db.HallPlanRegistrations
            .AnyAsync(r => r.PlayerId == currentUser.Id && r.HallPlanId == court1.HallPlanId);

        if (!isRegistered && !User.IsInRole("Admin")) 
            return StatusCode(StatusCodes.Status403Forbidden, "You must be registered to this Hallplan to swap players.");

        var playerCourt1 = await _db.HallEntities
            .Include(pc => pc.Player)
            .Include(pc => pc.HallPlanDay)
            .FirstOrDefaultAsync(pc => pc.Player.Id == player1Id && pc.HallPlanDay.Id == court1Id);

        var playerCourt2 = await _db.HallEntities
            .Include(pc => pc.Player)
            .Include(pc => pc.HallPlanDay)
            .FirstOrDefaultAsync(pc => pc.Player.Id == player2Id && pc.HallPlanDay.Id == court2Id);

        if (playerCourt1 == null || playerCourt2 == null)
        {
            return BadRequest("One or both players not found in specified courts.");
        }

        // Remove both entries from the database
        _db.HallEntities.Remove(playerCourt1);
        _db.HallEntities.Remove(playerCourt2);
        await _db.SaveChangesAsync();

        // Re-add entries with swapped court and player assignments
        _db.HallEntities.Add(new HallEntity
        {
            Player = _db.Players.Single(x => x.Id == player2Id),
            HallPlanDay = _db.Court.Single(x => x.Id == court1Id)
        });

        _db.HallEntities.Add(new HallEntity
        {
            Player = _db.Players.Single(x => x.Id == player1Id),
            HallPlanDay = _db.Court.Single(x => x.Id == court2Id)
        });

        await _db.SaveChangesAsync();
        return Ok();
    }
}
