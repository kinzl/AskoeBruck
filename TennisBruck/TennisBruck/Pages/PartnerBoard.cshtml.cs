using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TennisDb;

namespace TennisBruck.Pages;

[Authorize]
public class PartnerBoardModel(
    TennisContext db,
    UserManager<IdentityUser> userManager,
    CurrentPlayerService currentPlayerService,
    IEmailSender emailSender) : PageModel
{
    public Dictionary<DateTime, List<AvailabilitySlot>> SlotsByDay { get; set; } = new();
    public int CurrentPlayerId { get; set; }
    public List<AvailabilitySlot> MyFixedMatches { get; set; } = [];
    public List<AvailabilitySlot> MyOpenSlots { get; set; } = [];
    public List<AvailabilitySlot> OtherOpenSlots { get; set; } = [];

    [BindProperty(SupportsGet = true)] public DateTime? FilterDateFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? FilterDateTo { get; set; }
    [BindProperty(SupportsGet = true)] public TimeSpan? FilterTimeFrom { get; set; }
    [BindProperty(SupportsGet = true)] public TimeSpan? FilterTimeTo { get; set; }

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

        var query = db.AvailabilitySlots
            .Include(s => s.Player)
            .Include(s => s.MatchedWithPlayer)
            .Include(s => s.MatchedWithPlayer2)
            .Include(s => s.MatchedWithPlayer3)
            .Where(s => s.Date >= DateTime.Today && s.IsMatched == false)
            .AsQueryable();

        // Apply filters
        if (FilterDateFrom.HasValue) query = query.Where(s => s.Date >= FilterDateFrom.Value);
        if (FilterDateTo.HasValue) query = query.Where(s => s.Date <= FilterDateTo.Value);
        if (FilterTimeFrom.HasValue) query = query.Where(s => s.StartTime >= FilterTimeFrom.Value || s.EndTime > FilterTimeFrom.Value);
        if (FilterTimeTo.HasValue) query = query.Where(s => s.EndTime <= FilterTimeTo.Value || s.StartTime < FilterTimeTo.Value);

        var availableSlots = await query
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .ToListAsync();

        MyOpenSlots = availableSlots.Where(s => s.PlayerId == CurrentPlayerId).ToList();
        OtherOpenSlots = availableSlots.Where(s => s.PlayerId != CurrentPlayerId).ToList();

        SlotsByDay = OtherOpenSlots
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
        var joiningPlayer = await db.Players.Include(p => p.IdentityUser).FirstOrDefaultAsync(p => p.Id == CurrentPlayerId);

        var slot = await db.AvailabilitySlots
            .Include(s => s.Player).ThenInclude(p => p.IdentityUser)
            .FirstOrDefaultAsync(s => s.Id == slotId);

        if (slot == null)
        {
            TempData["ErrorMessage"] = "Dieser Eintrag existiert leider nicht mehr.";
            return RedirectToPage();
        }

        // Sicherheits-Check: Nur fixieren, wenn frei
        if (!slot.IsMatched)
        {
            if (CurrentPlayerId == slot.PlayerId || 
                CurrentPlayerId == slot.MatchedWithPlayerId || 
                CurrentPlayerId == slot.MatchedWithPlayer2Id || 
                CurrentPlayerId == slot.MatchedWithPlayer3Id)
            {
                // Cannot join own match or join twice
                return RedirectToPage();
            }

            int filledSlots = 0;
            if (slot.MatchedWithPlayerId == null) { slot.MatchedWithPlayerId = CurrentPlayerId; filledSlots++; }
            else if (slot.MatchedWithPlayer2Id == null) { slot.MatchedWithPlayer2Id = CurrentPlayerId; filledSlots++; }
            else if (slot.MatchedWithPlayer3Id == null) { slot.MatchedWithPlayer3Id = CurrentPlayerId; filledSlots++; }

            int totalJoined = 0;
            if (slot.MatchedWithPlayerId != null) totalJoined++;
            if (slot.MatchedWithPlayer2Id != null) totalJoined++;
            if (slot.MatchedWithPlayer3Id != null) totalJoined++;

            if (totalJoined >= slot.NeededPlayers)
            {
                slot.IsMatched = true; // Alle Slots vollendet!
                TempData["SuccessMessage"] = "Match komplett fixiert! Vergiss nicht, euch im Hallenplan noch einen Platz zu reservieren.";
            }
            else
            {
                TempData["SuccessMessage"] = "Du wurdest erfolgreich als Mitspieler eingetragen. Es fehlen noch weitere Spieler.";
            }

            await db.SaveChangesAsync();

            // Sende E-Mail an den Ersteller
            if (slot.Player?.IdentityUser?.Email != null && joiningPlayer != null)
            {
                var emailSubject = "🎾 Neuer Mitspieler in der Börse!";
                var emailBody = $"Hallo {slot.Player.Firstname},<br><br>" +
                                $"<strong>{joiningPlayer.Firstname} {joiningPlayer.Lastname}</strong> hat sich gerade für deinen Börsen-Eintrag am <strong>{slot.Date:dd.MM.yyyy}</strong> um {slot.StartTime:hh\\:mm} Uhr eingetragen!<br><br>";

                if (slot.IsMatched)
                {
                    emailBody += "<strong>Dein Match ist nun komplett fixiert!</strong> Vergiss nicht, euch rechtzeitig im Hallenplan einen Platz zu reservieren.<br><br>";
                }

                emailBody += "Viel Spaß beim Spielen!<br>Dein TennisBruck-Team";

                await emailSender.SendEmailAsync(slot.Player.IdentityUser.Email, emailSubject, emailBody);
            }
        }

        return RedirectToPage();
    }

    // Wird aufgerufen, wenn jemand auf "Speichern & Veröffentlichen" klickt
    public async Task<IActionResult> OnPostCreateSlotAsync(DateTime date, TimeSpan startTime, TimeSpan endTime,
        string message, int neededPlayers)
    {
        if (startTime >= endTime || startTime == endTime)
        {
            TempData["ErrorMessage"] = "Die Startzeit muss zwingend vor der Endzeit liegen.";
            return RedirectToPage();
        }

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
            NeededPlayers = neededPlayers,
            IsDouble = neededPlayers > 1, // Fallback für ältere Code-Teile
            IsMatched = false
        };

        db.AvailabilitySlots.Add(newSlot);
        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Dein Eintrag wurde erfolgreich veröffentlicht.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditSlotAsync(int editSlotId, DateTime editDate, TimeSpan editStartTime, TimeSpan editEndTime,
        string editMessage, int editNeededPlayers)
    {
        if (editStartTime >= editEndTime || editStartTime == editEndTime)
        {
            TempData["ErrorMessage"] = "Die Startzeit muss zwingend vor der Endzeit liegen.";
            return RedirectToPage();
        }

        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var dbUser = db.Players.Include(x => x.IdentityUser)
            .SingleOrDefault(x => x.IdentityUser != null && x.IdentityUser.Id == user.Id);
        if (dbUser == null) return Challenge();

        var slot = await db.AvailabilitySlots.FindAsync(editSlotId);
        if (slot == null)
        {
            TempData["ErrorMessage"] = "Dieser Eintrag existiert nicht mehr.";
            return RedirectToPage();
        }

        if (slot.PlayerId != dbUser.Id)
        {
            return Forbid();
        }

        slot.Date = editDate;
        slot.StartTime = editStartTime;
        slot.EndTime = editEndTime;
        slot.Message = editMessage;
        slot.NeededPlayers = editNeededPlayers;
        slot.IsDouble = editNeededPlayers > 1;

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Dein Eintrag wurde erfolgreich aktualisiert.";
        return RedirectToPage();
    }


    public async Task<IActionResult> OnPostDeleteSlotAsync(int slotId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var dbUser = db.Players.Include(x => x.IdentityUser)
            .SingleOrDefault(x => x.IdentityUser != null && x.IdentityUser.Id == user.Id);
        if (dbUser == null) return Challenge();

        var slot = await db.AvailabilitySlots
            .Include(s => s.Player).ThenInclude(p => p.IdentityUser)
            .FirstOrDefaultAsync(s => s.Id == slotId);

        if (slot == null)
        {
            TempData["ErrorMessage"] = "Dieser Eintrag existiert leider nicht mehr.";
            return RedirectToPage();
        }

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

            // Sende E-Mail an den Ersteller
            if (slot.Player?.IdentityUser?.Email != null)
            {
                var emailSubject = "🎾 Ein Mitspieler hat abgesagt!";
                var emailBody = $"Hallo {slot.Player.Firstname},<br><br>" +
                                $"<strong>{dbUser.Firstname} {dbUser.Lastname}</strong> hat sich gerade aus deinem Börsen-Eintrag am <strong>{slot.Date:dd.MM.yyyy}</strong> um {slot.StartTime:hh\\:mm} Uhr ausgetragen.<br>" +
                                $"Der freie Platz wurde automatisch wieder in die Börse gestellt.<br><br>" +
                                "Dein TennisBruck-Team";

                await emailSender.SendEmailAsync(slot.Player.IdentityUser.Email, emailSubject, emailBody);
            }
        }

        await db.SaveChangesAsync();

        return RedirectToPage();
    }
}