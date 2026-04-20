using Microsoft.AspNetCore.Identity;

namespace TennisBruck.Pages;

[Authorize]
public class PartnerBoardModel(
    TennisContext db,
    UserManager<IdentityUser> userManager,
    CurrentPlayerService currentPlayerService) : PageModel
{
    public Dictionary<DateTime, List<AvailabilitySlot>> SlotsByDay { get; set; } = new();
    public int CurrentPlayerId { get; set; }
    public List<AvailabilitySlot> MyFixedMatches { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        CurrentPlayerId = currentPlayerService.GetCurrentUser()!.Id;

        var oldSlots = await db.AvailabilitySlots
            .Where(s => s.Date < DateTime.Today)
            .ToListAsync();

        if (oldSlots.Any())
        {
            db.AvailabilitySlots.RemoveRange(oldSlots);
            await db.SaveChangesAsync();
        }

        var availableSlots = await db.AvailabilitySlots
            .Include(s => s.Player)
            .Include(s => s.MatchedWithPlayer)
            .Include(s => s.MatchedWithPlayer2)
            .Include(s => s.MatchedWithPlayer3)
            .Where(s => s.Date >= DateTime.Today && s.IsMatched == false)
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .ToListAsync();

        SlotsByDay = availableSlots
            .GroupBy(s => s.Date.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        // B) Hole MEINE FIXIERTEN Matches (Ich bin Ersteller ODER ich habe angenommen)
        if (CurrentPlayerId > 0)
        {
            MyFixedMatches = await db.AvailabilitySlots
                .Include(s => s.Player) // Lade den Ersteller mit
                .Include(s => s.MatchedWithPlayer)
                .Include(s => s.MatchedWithPlayer2)
                .Include(s => s.MatchedWithPlayer3)
                .Where(s => s.Date >= DateTime.Today && s.IsMatched == true &&
                            (s.PlayerId == CurrentPlayerId || 
                             s.MatchedWithPlayerId == CurrentPlayerId ||
                             s.MatchedWithPlayer2Id == CurrentPlayerId ||
                             s.MatchedWithPlayer3Id == CurrentPlayerId))
                .OrderBy(s => s.Date)
                .ThenBy(s => s.StartTime)
                .ToListAsync();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAcceptMatchAsync(int slotId)
    {
        CurrentPlayerId = currentPlayerService.GetCurrentUser()!.Id;

        var slot = await db.AvailabilitySlots
            .Include(s => s.Player)
            .FirstOrDefaultAsync(s => s.Id == slotId);

        // Sicherheits-Check: Nur fixieren, wenn frei
        if (slot != null && !slot.IsMatched)
        {
            if (CurrentPlayerId == slot.PlayerId || 
                CurrentPlayerId == slot.MatchedWithPlayerId || 
                CurrentPlayerId == slot.MatchedWithPlayer2Id || 
                CurrentPlayerId == slot.MatchedWithPlayer3Id)
            {
                // Cannot join own match or join twice
                return RedirectToPage();
            }

            if (!slot.IsDouble)
            {
                slot.MatchedWithPlayerId = CurrentPlayerId;
                slot.IsMatched = true;
                TempData["SuccessMessage"] = "Einzel-Match erfolgreich fixiert! Vergiss nicht, euch im Hallenplan noch einen Platz zu reservieren.";
            }
            else
            {
                if (slot.MatchedWithPlayerId == null) slot.MatchedWithPlayerId = CurrentPlayerId;
                else if (slot.MatchedWithPlayer2Id == null) slot.MatchedWithPlayer2Id = CurrentPlayerId;
                else if (slot.MatchedWithPlayer3Id == null)
                {
                    slot.MatchedWithPlayer3Id = CurrentPlayerId;
                    slot.IsMatched = true; // Alle 3 Slots vollendet!
                    TempData["SuccessMessage"] = "Doppel-Match komplett fixiert! Vergiss nicht, euch im Hallenplan noch einen Platz zu reservieren.";
                }
                
                if (TempData["SuccessMessage"] == null)
                {
                    TempData["SuccessMessage"] = "Du wurdest erfolgreich als Mitspieler für das Doppel eingetragen. Es fehlen noch weitere Spieler.";
                }
            }

            await db.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    // Wird aufgerufen, wenn jemand auf "Speichern & Veröffentlichen" klickt
    public async Task<IActionResult> OnPostCreateSlotAsync(DateTime date, TimeSpan startTime, TimeSpan endTime,
        string message, bool isDouble)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge(); // Zur Login-Seite, falls nicht eingeloggt
        var dbUser = db.Players.Include(x => x.IdentityUser)
            .SingleOrDefault(x => x.IdentityUser != null && x.IdentityUser.Id == user.Id);
        if (dbUser == null) return Challenge();

        var newSlot = new AvailabilitySlot
        {
            PlayerId = dbUser.Id,
            Date = date,
            StartTime = startTime,
            EndTime = endTime,
            Message = message,
            IsDouble = isDouble,
            IsMatched = false
        };

        db.AvailabilitySlots.Add(newSlot);
        await db.SaveChangesAsync();

        return RedirectToPage();
    }


    public async Task<IActionResult> OnPostDeleteSlotAsync(int slotId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var slot = await db.AvailabilitySlots.FindAsync(slotId);
        var dbUser = db.Players.Include(x => x.IdentityUser)
            .SingleOrDefault(x => x.IdentityUser != null && x.IdentityUser.Id == user.Id);
        if (dbUser == null) return Challenge();
        if (slot == null) return RedirectToPage();

        if (slot.PlayerId == dbUser.Id)
        {
            db.AvailabilitySlots.Remove(slot);
            TempData["SuccessMessage"] = "Dein Eintrag wurde erfolgreich gelöscht.";
        }
        else
        {
            if (slot.MatchedWithPlayerId == dbUser.Id) slot.MatchedWithPlayerId = null;
            if (slot.MatchedWithPlayer2Id == dbUser.Id) slot.MatchedWithPlayer2Id = null;
            if (slot.MatchedWithPlayer3Id == dbUser.Id) slot.MatchedWithPlayer3Id = null;

            slot.IsMatched = false;
            TempData["SuccessMessage"] = "Du hast dich aus dem Match ausgetragen. Der freie Platz wurde wieder in die Börse gestellt.";
        }

        await db.SaveChangesAsync();

        return RedirectToPage();
    }
}