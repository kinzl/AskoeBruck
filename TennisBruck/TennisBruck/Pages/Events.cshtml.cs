using System.Text;
using TennisDb;

namespace TennisBruck.Pages;

public class EventsModel(ClubEventService eventService) : PageModel
{
    public string? Message { get; set; }
    public string ActiveTab { get; set; } = "events"; // "events" or "matches"
    public string SelectedMatchCategory { get; set; } = "Alle";

    public List<ClubEvent> PinnedEvents { get; set; } = [];
    public List<ClubEvent> UpcomingEvents { get; set; } = [];
    public List<ClubEvent> PastEvents { get; set; } = [];

    public List<OetvMatch> UpcomingMatches { get; set; } = [];
    public List<OetvMatch> PastMatches { get; set; } = [];
    public List<string> AvailableMatchCategories { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(
        string? activeTab = "events",
        string? matchCategory = "Alle",
        string? message = null)
    {
        ActiveTab = string.IsNullOrWhiteSpace(activeTab) ? "events" : activeTab;
        SelectedMatchCategory = string.IsNullOrWhiteSpace(matchCategory) ? "Alle" : matchCategory;
        Message = message;

        // Fetch dynamic categories that actually have competitions/matches in the system
        AvailableMatchCategories = await eventService.GetAvailableMatchCategoriesAsync();
        if (SelectedMatchCategory != "Alle" && !AvailableMatchCategories.Contains(SelectedMatchCategory))
        {
            SelectedMatchCategory = "Alle";
        }

        // Notice Board shows all notices without category filtering
        var allUpcomingEvents = await eventService.GetUpcomingEventsAsync("Alle");
        PinnedEvents = allUpcomingEvents.Where(e => e.IsPinned).ToList();
        UpcomingEvents = allUpcomingEvents.Where(e => !e.IsPinned).ToList();
        PastEvents = await eventService.GetPastEventsAsync("Alle");

        // ÖTV Matches filtered by selected category
        UpcomingMatches = await eventService.GetUpcomingTeamMatchesAsync(SelectedMatchCategory);
        PastMatches = await eventService.GetPastTeamMatchesAsync(SelectedMatchCategory);

        return Page();
    }

    #region Event & Notice Actions (Admin)

    public async Task<IActionResult> OnPostCreateEventAsync(ClubEvent newEvent, IFormFile? imageFile, IFormFile? pdfFile)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        if (string.IsNullOrWhiteSpace(newEvent.Category))
        {
            newEvent.Category = "Aushang";
        }
        if (string.IsNullOrWhiteSpace(newEvent.Location))
        {
            newEvent.Location = "";
        }
        if (newEvent.StartDate == default)
        {
            newEvent.StartDate = DateTime.UtcNow;
        }

        await eventService.CreateEventAsync(newEvent, imageFile, pdfFile);
        return RedirectToPage(new { activeTab = "events", Message = "Aushang wurde erfolgreich veröffentlicht!" });
    }

    public async Task<IActionResult> OnPostUpdateEventAsync(ClubEvent editEvent, IFormFile? imageFile, IFormFile? pdfFile)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        if (string.IsNullOrWhiteSpace(editEvent.Category))
        {
            editEvent.Category = "Aushang";
        }

        await eventService.UpdateEventAsync(editEvent, imageFile, pdfFile);
        return RedirectToPage(new { activeTab = "events", Message = "Aushang wurde erfolgreich aktualisiert!" });
    }

    public async Task<IActionResult> OnPostDeleteEventAsync(int id)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        await eventService.DeleteEventAsync(id);
        return RedirectToPage(new { activeTab = "events", Message = "Event wurde gelöscht." });
    }

    public async Task<IActionResult> OnPostTogglePinEventAsync(int id)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        await eventService.TogglePinEventAsync(id);
        return RedirectToPage(new { activeTab = "events" });
    }

    #endregion

    #region Team Match Actions (Admin)

    public async Task<IActionResult> OnPostSyncOetvMatchesAsync(string? clubIdentifier)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        try
        {
            var count = await eventService.SyncOetvMatchesFromFederationAsync(clubIdentifier ?? "40291");
            string msg = count > 0
                ? $"ÖTV-Synchronisation erfolgreich: {count} Meisterschaftsspiele wurden vom Verband importiert!"
                : "Unter der angegebenen Kennung/Adresse konnten keine Meisterschaftsspiele gefunden werden. Bitte prüfe die Eingabe (z. B. Vereinsnummer 40291).";

            return RedirectToPage(new
            {
                activeTab = "matches",
                Message = msg
            });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new
            {
                activeTab = "matches",
                Message = $"Fehler bei der ÖTV-Synchronisation: {ex.Message}"
            });
        }
    }

    public async Task<IActionResult> OnPostClearMatchesAsync()
    {
        if (!User.IsInRole("Admin")) return Forbid();

        await eventService.ClearAllMatchesAsync();
        return RedirectToPage(new
        {
            activeTab = "matches",
            Message = "Alle Meisterschaftsspiele wurden gelöscht."
        });
    }

    public async Task<IActionResult> OnPostCreateTeamMatchAsync(OetvMatch newMatch)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        await eventService.CreateTeamMatchAsync(newMatch);
        return RedirectToPage(new { activeTab = "matches", Message = "ÖTV Meisterschaftsspiel erfolgreich eingetragen!" });
    }

    public async Task<IActionResult> OnPostUpdateTeamMatchAsync(OetvMatch editMatch)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        await eventService.UpdateTeamMatchAsync(editMatch);
        return RedirectToPage(new { activeTab = "matches", Message = "Spiel erfolgreich aktualisiert!" });
    }

    public async Task<IActionResult> OnPostDeleteTeamMatchAsync(int id)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        await eventService.DeleteTeamMatchAsync(id);
        return RedirectToPage(new { activeTab = "matches", Message = "Spiel wurde gelöscht." });
    }

    public async Task<IActionResult> OnPostUpdateScoreAsync(int id, string score)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        await eventService.UpdateScoreAsync(id, score);
        return RedirectToPage(new { activeTab = "matches", Message = "Ergebnis gespeichert!" });
    }

    #endregion

    #region Calendar Export

    public async Task<IActionResult> OnGetDownloadEventIcsAsync(int id)
    {
        var ev = await eventService.GetEventByIdAsync(id);
        if (ev == null) return NotFound();

        var ics = eventService.GenerateEventIcs(ev);
        var bytes = Encoding.UTF8.GetBytes(ics);
        var cleanTitle = string.Concat(ev.Title.Split(Path.GetInvalidFileNameChars()));
        return File(bytes, "text/calendar", $"{cleanTitle}.ics");
    }

    public async Task<IActionResult> OnGetDownloadMatchIcsAsync(int id)
    {
        var match = await eventService.GetTeamMatchByIdAsync(id);
        if (match == null) return NotFound();

        var ics = eventService.GenerateMatchIcs(match);
        var bytes = Encoding.UTF8.GetBytes(ics);
        return File(bytes, "text/calendar", $"match-{match.HomeTeam}-vs-{match.AwayTeam}.ics");
    }

    #endregion
}
