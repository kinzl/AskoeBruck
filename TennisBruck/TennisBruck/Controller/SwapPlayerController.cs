using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TennisDb;

namespace TennisBruck.Controller;

[Authorize]
[Route("[controller]/[action]")]
public class SwapPlayerController : ControllerBase
{
    private TennisContext _db;

    public SwapPlayerController(TennisContext db)
    {
        _db = db;
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
        return Ok();
    }
}
