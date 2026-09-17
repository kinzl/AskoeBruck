using System.Text;

namespace TennisBruck.Features.Events;

public class ClubEventService(
    TennisContext db,
    IWebHostEnvironment? env,
    ILogger<ClubEventService> logger,
    OetvScraperService? scraperService = null)
{
    #region Club Events

    public async Task<List<ClubEvent>> GetUpcomingEventsAsync(string? category = null)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-1);
        var query = db.ClubEvents
            .Where(e => e.StartDate >= cutoff || e.IsPinned);

        if (!string.IsNullOrWhiteSpace(category) && category != "Alle")
        {
            query = query.Where(e => e.Category == category);
        }

        return await query
            .OrderByDescending(e => e.IsPinned)
            .ThenBy(e => e.StartDate)
            .ToListAsync();
    }

    public async Task<List<ClubEvent>> GetPastEventsAsync(string? category = null)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-1);
        var query = db.ClubEvents
            .Where(e => e.StartDate < cutoff && !e.IsPinned);

        if (!string.IsNullOrWhiteSpace(category) && category != "Alle")
        {
            query = query.Where(e => e.Category == category);
        }

        return await query
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();
    }

    public async Task<ClubEvent?> GetEventByIdAsync(int id)
    {
        return await db.ClubEvents.FindAsync(id);
    }

    public async Task CreateEventAsync(ClubEvent ev, IFormFile? imageFile, IFormFile? pdfFile)
    {
        if (imageFile != null && imageFile.Length > 0)
        {
            ev.ImageUrl = await SaveFileAsync(imageFile, "images");
        }

        if (pdfFile != null && pdfFile.Length > 0)
        {
            ev.AttachmentUrl = await SaveFileAsync(pdfFile, "docs");
            ev.AttachmentName = pdfFile.FileName;
        }

        db.ClubEvents.Add(ev);
        await db.SaveChangesAsync();
        logger.LogInformation("Created ClubEvent {Title} (Id: {Id})", ev.Title, ev.Id);
    }

    public async Task UpdateEventAsync(ClubEvent ev, IFormFile? imageFile, IFormFile? pdfFile)
    {
        var existing = await db.ClubEvents.FindAsync(ev.Id);
        if (existing == null) return;

        existing.Title = ev.Title;
        existing.Description = ev.Description;
        existing.Category = ev.Category;
        existing.StartDate = ev.StartDate;
        existing.EndDate = ev.EndDate;
        existing.Location = ev.Location;
        existing.IsPinned = ev.IsPinned;

        if (imageFile != null && imageFile.Length > 0)
        {
            existing.ImageUrl = await SaveFileAsync(imageFile, "images");
        }

        if (pdfFile != null && pdfFile.Length > 0)
        {
            existing.AttachmentUrl = await SaveFileAsync(pdfFile, "docs");
            existing.AttachmentName = pdfFile.FileName;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Updated ClubEvent {Title} (Id: {Id})", existing.Title, existing.Id);
    }

    public async Task DeleteEventAsync(int id)
    {
        var ev = await db.ClubEvents.FindAsync(id);
        if (ev != null)
        {
            db.ClubEvents.Remove(ev);
            await db.SaveChangesAsync();
            logger.LogInformation("Deleted ClubEvent {Id}", id);
        }
    }

    public async Task TogglePinEventAsync(int id)
    {
        var ev = await db.ClubEvents.FindAsync(id);
        if (ev != null)
        {
            ev.IsPinned = !ev.IsPinned;
            await db.SaveChangesAsync();
        }
    }

    #endregion

    #region ÖTV & Team League Matches

    public async Task<List<OetvMatch>> GetUpcomingTeamMatchesAsync(string? category = null)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-1);
        var query = db.OetvMatches
            .Where(m => m.MatchDateTime >= cutoff && m.Status != "Beendet");

        if (!string.IsNullOrWhiteSpace(category) && category != "Alle")
        {
            query = query.Where(m => m.Category == category);
        }

        return await query
            .OrderBy(m => m.MatchDateTime)
            .ToListAsync();
    }

    public async Task<List<OetvMatch>> GetPastTeamMatchesAsync(string? category = null)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-1);
        var query = db.OetvMatches
            .Where(m => m.MatchDateTime < cutoff || m.Status == "Beendet");

        if (!string.IsNullOrWhiteSpace(category) && category != "Alle")
        {
            query = query.Where(m => m.Category == category);
        }

        return await query
            .OrderByDescending(m => m.MatchDateTime)
            .ToListAsync();
    }

    public async Task<List<string>> GetAvailableMatchCategoriesAsync()
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-1);
        // Only return categories that have active/upcoming matches scheduled
        var categories = await db.OetvMatches
            .Where(m => !string.IsNullOrEmpty(m.Category) && m.MatchDateTime >= cutoff && m.Status != "Beendet")
            .Select(m => m.Category)
            .Distinct()
            .ToListAsync();

        // If no upcoming matches exist at all in the system, fall back to categories from past matches
        if (!categories.Any())
        {
            categories = await db.OetvMatches
                .Where(m => !string.IsNullOrEmpty(m.Category))
                .Select(m => m.Category)
                .Distinct()
                .ToListAsync();
        }

        var preferredOrder = new[] { "Herren", "Damen", "Jugend / Kids", "Senioren", "Mixed" };
        return categories
            .OrderBy(c =>
            {
                int idx = Array.IndexOf(preferredOrder, c);
                return idx >= 0 ? idx : 99;
            })
            .ThenBy(c => c)
            .ToList();
    }

    public async Task<OetvMatch?> GetTeamMatchByIdAsync(int id)
    {
        return await db.OetvMatches.FindAsync(id);
    }

    public async Task CreateTeamMatchAsync(OetvMatch match)
    {
        db.OetvMatches.Add(match);
        await db.SaveChangesAsync();
        logger.LogInformation("Created OetvMatch {Home} vs {Away} (Id: {Id})", match.HomeTeam, match.AwayTeam, match.Id);
    }

    public async Task UpdateTeamMatchAsync(OetvMatch match)
    {
        var existing = await db.OetvMatches.FindAsync(match.Id);
        if (existing == null) return;

        existing.CompetitionName = match.CompetitionName;
        existing.Category = match.Category;
        existing.HomeTeam = match.HomeTeam;
        existing.AwayTeam = match.AwayTeam;
        existing.IsHomeMatch = match.IsHomeMatch;
        existing.MatchDateTime = match.MatchDateTime;
        existing.Venue = match.Venue;
        existing.Status = match.Status;
        existing.Score = match.Score;
        existing.OetvUrl = match.OetvUrl;
        existing.Notes = match.Notes;

        await db.SaveChangesAsync();
        logger.LogInformation("Updated OetvMatch {Id}", match.Id);
    }

    public async Task DeleteTeamMatchAsync(int id)
    {
        var match = await db.OetvMatches.FindAsync(id);
        if (match != null)
        {
            db.OetvMatches.Remove(match);
            await db.SaveChangesAsync();
            logger.LogInformation("Deleted OetvMatch {Id}", id);
        }
    }

    public async Task ClearAllMatchesAsync()
    {
        db.OetvMatches.RemoveRange(db.OetvMatches);
        await db.SaveChangesAsync();
        logger.LogInformation("Cleared all OetvMatches from database");
    }

    public async Task UpdateScoreAsync(int id, string score, string status = "Beendet")
    {
        var match = await db.OetvMatches.FindAsync(id);
        if (match != null)
        {
            match.Score = score;
            match.Status = status;
            await db.SaveChangesAsync();
        }
    }

    public async Task<int> SyncOetvMatchesFromFederationAsync(string? clubIdentifier = "40291")
    {
        if (scraperService == null)
        {
            logger.LogWarning("OetvScraperService is not configured");
            return 0;
        }

        var fetchedMatches = await scraperService.FetchClubMatchesAsync(clubIdentifier);
        if (!fetchedMatches.Any()) return 0;

        int count = 0;
        foreach (var m in fetchedMatches)
        {
            var matchUtc = DateTime.SpecifyKind(m.MatchDateTime, DateTimeKind.Utc);
            m.MatchDateTime = matchUtc;
            var dayStart = matchUtc.Date;
            var dayEnd = dayStart.AddDays(1);

            var existing = await db.OetvMatches
                .FirstOrDefaultAsync(x =>
                    x.HomeTeam == m.HomeTeam &&
                    x.AwayTeam == m.AwayTeam &&
                    x.MatchDateTime >= dayStart &&
                    x.MatchDateTime < dayEnd);

            if (existing == null)
            {
                db.OetvMatches.Add(m);
                count++;
            }
            else
            {
                existing.CompetitionName = m.CompetitionName;
                existing.Category = m.Category;
                existing.Venue = m.Venue;
                existing.IsHomeMatch = m.IsHomeMatch;
                existing.OetvUrl = m.OetvUrl ?? existing.OetvUrl;
                if (!string.IsNullOrEmpty(m.Score)) existing.Score = m.Score;
                if (!string.IsNullOrEmpty(m.Status)) existing.Status = m.Status;
                count++;
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Synced {Count} ÖTV matches for club {Club}", count, clubIdentifier);
        return count;
    }

    #endregion

    #region iCalendar (.ics) Export

    public string GenerateEventIcs(ClubEvent ev)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//ASKÖ Bruck Tennis//Infoboard//DE");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:event-{ev.Id}-{ev.CreatedAt.Ticks}@tennisbruck.at");
        sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
        sb.AppendLine($"DTSTART:{ev.StartDate.ToUniversalTime():yyyyMMddTHHmmssZ}");

        var end = ev.EndDate ?? ev.StartDate.AddHours(2);
        sb.AppendLine($"DTEND:{end.ToUniversalTime():yyyyMMddTHHmmssZ}");
        sb.AppendLine($"SUMMARY:{EscapeIcs(ev.Title)}");
        sb.AppendLine($"DESCRIPTION:{EscapeIcs(ev.Description)}");
        sb.AppendLine($"LOCATION:{EscapeIcs(ev.Location)}");
        sb.AppendLine("STATUS:CONFIRMED");
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    public string GenerateMatchIcs(OetvMatch match)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//ASKÖ Bruck Tennis//ÖTV Matches//DE");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:match-{match.Id}-{match.CreatedAt.Ticks}@tennisbruck.at");
        sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
        sb.AppendLine($"DTSTART:{match.MatchDateTime.ToUniversalTime():yyyyMMddTHHmmssZ}");
        sb.AppendLine($"DTEND:{match.MatchDateTime.AddHours(3).ToUniversalTime():yyyyMMddTHHmmssZ}");

        string homeAway = match.IsHomeMatch ? "Heimspiel" : "Auswärtsspiel";
        string title = $"🎾 {match.HomeTeam} vs. {match.AwayTeam} ({match.Category})";
        string desc = $"Bewerb: {match.CompetitionName}\\nModus: {homeAway}\\nOrt: {match.Venue}";
        if (!string.IsNullOrWhiteSpace(match.Notes))
        {
            desc += $"\\nHinweis: {match.Notes}";
        }

        sb.AppendLine($"SUMMARY:{EscapeIcs(title)}");
        sb.AppendLine($"DESCRIPTION:{EscapeIcs(desc)}");
        sb.AppendLine($"LOCATION:{EscapeIcs(match.Venue)}");
        sb.AppendLine("STATUS:CONFIRMED");
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string EscapeIcs(string text)
    {
        return text
            .Replace(@"\", @"\\")
            .Replace(";", @"\;")
            .Replace(",", @"\,")
            .Replace("\r\n", @"\n")
            .Replace("\n", @"\n");
    }

    private async Task<string> SaveFileAsync(IFormFile file, string subFolder)
    {
        var uploadsDir = Path.Combine(env.WebRootPath, "uploads", subFolder);
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsDir, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/{subFolder}/{fileName}";
    }

    #endregion
}
