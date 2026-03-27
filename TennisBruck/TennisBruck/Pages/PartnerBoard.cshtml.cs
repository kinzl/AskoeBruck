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

    // 1. Neue Eigenschaft für die fixierten Matches hinzufügen (oben bei den anderen Properties)
    public List<AvailabilitySlot> MyFixedMatches { get; set; } = new();

// 2. Die OnGetAsync Methode updaten
    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user != null)
        {
            CurrentPlayerId = currentPlayerService.GetCurrentUser()!.Id;
        }

        var oldSlots = await db.AvailabilitySlots
            .Where(s => s.Date < DateTime.Today)
            .ToListAsync();

        if (oldSlots.Any()) // Wenn er welche findet...
        {
            db.AvailabilitySlots.RemoveRange(oldSlots); // ...alle auf einmal löschen
            await db.SaveChangesAsync(); // ...und ab in den Papierkorb!
        }

        // A) Hole die OFFENEN Slots (für das allgemeine Board)
        var availableSlots = await db.AvailabilitySlots
            .Include(s => s.Player)
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
                .Where(s => s.Date >= DateTime.Today && s.IsMatched == true &&
                            (s.PlayerId == CurrentPlayerId || s.MatchedWithPlayerId == CurrentPlayerId))
                .OrderBy(s => s.Date)
                .ThenBy(s => s.StartTime)
                .ToListAsync();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAcceptMatchAsync(int slotId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var currentPlayer = currentPlayerService.GetCurrentUser()!;

        var slot = await db.AvailabilitySlots
            .Include(s => s.Player)
            .FirstOrDefaultAsync(s => s.Id == slotId);

        // Sicherheits-Check: Nur fixieren, wenn frei und NICHT der eigene Termin
        if (slot != null && !slot.IsMatched && slot.PlayerId != currentPlayer.Id)
        {
            // 1. Match in der Börse als "Fixiert" markieren
            slot.IsMatched = true;
            slot.MatchedWithPlayerId = currentPlayer.Id;

            // 2. Neue Erfolgsmeldung mit einem kleinen Reminder
            TempData["SuccessMessage"] =
                "Match erfolgreich fixiert! Vergiss nicht, euch im Hallenplan noch einen Platz zu reservieren.";

            await db.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    // Wird aufgerufen, wenn jemand auf "Speichern & Veröffentlichen" klickt
    public async Task<IActionResult> OnPostCreateSlotAsync(DateTime date, TimeSpan startTime, TimeSpan endTime,
        string message)
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
        // Sicherheits-Check: Nur löschen, wenn der Slot existiert UND er wirklich von diesem User erstellt wurde!
        if (slot != null && slot.PlayerId == dbUser.Id)
        {
            db.AvailabilitySlots.Remove(slot);
            await db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Dein Eintrag wurde erfolgreich gelöscht.";
        }

        return RedirectToPage();
    }
}