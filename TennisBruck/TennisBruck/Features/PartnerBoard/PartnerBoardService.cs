using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using TennisDb;

namespace TennisBruck.Features.PartnerBoard;

public class PartnerBoardOverview
{
    public Dictionary<DateTime, List<AvailabilitySlot>> SlotsByDay { get; set; } = new();
    public List<AvailabilitySlot> MyFixedMatches { get; set; } = [];
    public List<AvailabilitySlot> MyOpenSlots { get; set; } = [];
    public List<AvailabilitySlot> OtherOpenSlots { get; set; } = [];
    public bool HasAnySlots { get; set; }
    public bool IsFilterActive { get; set; }
    public DateTime? FilterDateFrom { get; set; }
    public DateTime? FilterDateTo { get; set; }
    public TimeSpan? FilterTimeFrom { get; set; }
    public TimeSpan? FilterTimeTo { get; set; }
}

public class PartnerBoardService(TennisContext db, IEmailSender emailSender, WebPushNotificationService? pushService = null)
{
    public async Task<PartnerBoardOverview> GetOverviewAsync(
        int currentPlayerId,
        DateTime? filterDateFrom,
        DateTime? filterDateTo,
        TimeSpan? filterTimeFrom,
        TimeSpan? filterTimeTo)
    {
        await db.AvailabilitySlots
            .Where(s => s.Date < DateTime.Today)
            .ExecuteDeleteAsync();

        bool isFilterActive = filterDateFrom.HasValue || filterDateTo.HasValue ||
                              filterTimeFrom.HasValue || filterTimeTo.HasValue;

        if (filterDateFrom.HasValue && filterDateFrom.Value.Date < DateTime.Today)
            filterDateFrom = DateTime.Today;
        if (filterDateTo.HasValue && filterDateTo.Value.Date < DateTime.Today)
            filterDateTo = DateTime.Today;
        if (filterDateFrom.HasValue && filterDateTo.HasValue && filterDateTo.Value < filterDateFrom.Value)
            filterDateTo = filterDateFrom;
        if (filterTimeFrom.HasValue && filterTimeTo.HasValue && filterTimeTo.Value <= filterTimeFrom.Value)
            filterTimeTo = null;

        bool hasAnySlots = await db.AvailabilitySlots
            .AnyAsync(s => s.Date >= DateTime.Today && s.IsMatched == false && s.PlayerId != currentPlayerId);

        var query = db.AvailabilitySlots
            .Include(s => s.Player)
            .Include(s => s.MatchedWithPlayer)
            .Include(s => s.MatchedWithPlayer2)
            .Include(s => s.MatchedWithPlayer3)
            .Where(s => s.Date >= DateTime.Today && s.IsMatched == false)
            .AsQueryable();

        if (filterDateFrom.HasValue) query = query.Where(s => s.Date >= filterDateFrom.Value);
        if (filterDateTo.HasValue) query = query.Where(s => s.Date <= filterDateTo.Value);
        if (filterTimeFrom.HasValue)
            query = query.Where(s => s.StartTime >= filterTimeFrom.Value || s.EndTime > filterTimeFrom.Value);
        if (filterTimeTo.HasValue)
            query = query.Where(s => s.EndTime <= filterTimeTo.Value || s.StartTime < filterTimeTo.Value);

        var availableSlots = await query
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .ToListAsync();

        var myOpenSlots = availableSlots.Where(s => s.PlayerId == currentPlayerId).ToList();
        var otherOpenSlots = availableSlots.Where(s => s.PlayerId != currentPlayerId).ToList();

        var slotsByDay = otherOpenSlots
            .GroupBy(s => s.Date.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var myFixedMatches = new List<AvailabilitySlot>();
        if (currentPlayerId > 0)
        {
            myFixedMatches = await db.AvailabilitySlots
                .Include(s => s.Player)
                .Include(s => s.MatchedWithPlayer)
                .Include(s => s.MatchedWithPlayer2)
                .Include(s => s.MatchedWithPlayer3)
                .Where(s => s.Date >= DateTime.Today && s.IsMatched == true &&
                            (s.PlayerId == currentPlayerId ||
                             s.MatchedWithPlayerId == currentPlayerId ||
                             s.MatchedWithPlayer2Id == currentPlayerId ||
                             s.MatchedWithPlayer3Id == currentPlayerId))
                .OrderBy(s => s.Date)
                .ThenBy(s => s.StartTime)
                .ToListAsync();
        }

        return new PartnerBoardOverview
        {
            SlotsByDay = slotsByDay,
            MyFixedMatches = myFixedMatches,
            MyOpenSlots = myOpenSlots,
            OtherOpenSlots = otherOpenSlots,
            HasAnySlots = hasAnySlots,
            IsFilterActive = isFilterActive,
            FilterDateFrom = filterDateFrom,
            FilterDateTo = filterDateTo,
            FilterTimeFrom = filterTimeFrom,
            FilterTimeTo = filterTimeTo
        };
    }

    public async Task<(bool Success, string Message)> AcceptMatchAsync(int slotId, int joiningPlayerId)
    {
        var joiningPlayer = await db.Players.Include(p => p.IdentityUser).FirstOrDefaultAsync(p => p.Id == joiningPlayerId);

        var slot = await db.AvailabilitySlots
            .Include(s => s.Player).ThenInclude(p => p.IdentityUser)
            .Include(s => s.Player).ThenInclude(p => p.NotificationSettings)
            .Include(s => s.MatchedWithPlayer).ThenInclude(p => p.IdentityUser)
            .Include(s => s.MatchedWithPlayer).ThenInclude(p => p.NotificationSettings)
            .Include(s => s.MatchedWithPlayer2).ThenInclude(p => p.IdentityUser)
            .Include(s => s.MatchedWithPlayer2).ThenInclude(p => p.NotificationSettings)
            .Include(s => s.MatchedWithPlayer3).ThenInclude(p => p.IdentityUser)
            .Include(s => s.MatchedWithPlayer3).ThenInclude(p => p.NotificationSettings)
            .FirstOrDefaultAsync(s => s.Id == slotId);

        if (slot == null)
            return (false, "Dieser Eintrag existiert leider nicht mehr.");

        if (slot.IsMatched)
            return (false, "Dieses Spiel ist bereits vergeben.");

        if (joiningPlayerId == slot.PlayerId ||
            joiningPlayerId == slot.MatchedWithPlayerId ||
            joiningPlayerId == slot.MatchedWithPlayer2Id ||
            joiningPlayerId == slot.MatchedWithPlayer3Id)
        {
            return (false, "Du bist bereits in diesem Eintrag eingetragen.");
        }

        if (slot.MatchedWithPlayerId == null)
        {
            slot.MatchedWithPlayerId = joiningPlayerId;
        }
        else if (slot.MatchedWithPlayer2Id == null)
        {
            slot.MatchedWithPlayer2Id = joiningPlayerId;
        }
        else if (slot.MatchedWithPlayer3Id == null)
        {
            slot.MatchedWithPlayer3Id = joiningPlayerId;
        }

        int totalJoined = 0;
        if (slot.MatchedWithPlayerId != null) totalJoined++;
        if (slot.MatchedWithPlayer2Id != null) totalJoined++;
        if (slot.MatchedWithPlayer3Id != null) totalJoined++;

        string message;
        if (totalJoined >= slot.NeededPlayers)
        {
            slot.IsMatched = true;
            message = "Match komplett fixiert! Vergiss nicht, euch im Hallenplan noch einen Platz zu reservieren.";
        }
        else
        {
            message = "Du wurdest erfolgreich als Mitspieler eingetragen. Es fehlen noch weitere Spieler.";
        }

        await db.SaveChangesAsync();

        bool creatorAlreadyNotified = false;
        if (slot.Player.IdentityUser?.Email != null && joiningPlayer != null)
        {
            if (slot.Player.NotificationSettings?.EmailOnSlotJoined == true)
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
                creatorAlreadyNotified = true;
            }

            if (pushService != null && slot.Player != null)
            {
                await pushService.SendNotificationAsync(slot.Player.Id, "🎾 Neuer Mitspieler in der Börse!", $"{joiningPlayer.Firstname} {joiningPlayer.Lastname} spielt am {slot.Date:dd.MM.yyyy} mit.", "/PartnerBoard");
            }
        }

        if (slot.IsMatched)
        {
            var participants = new List<Player> { slot.Player! };
            if (slot.MatchedWithPlayer != null) participants.Add(slot.MatchedWithPlayer);
            if (slot.MatchedWithPlayer2 != null) participants.Add(slot.MatchedWithPlayer2);
            if (slot.MatchedWithPlayer3 != null) participants.Add(slot.MatchedWithPlayer3);

            var playerNames = string.Join(", ", participants.Select(p => $"{p.Firstname} {p.Lastname}"));

            foreach (var p in participants)
            {
                if (creatorAlreadyNotified && p.Id == slot.PlayerId) continue;

                if (p.IdentityUser?.Email != null &&
                    (p.NotificationSettings == null || p.NotificationSettings.EmailOnSlotFull))
                {
                    var subject = "🎾 Börsen-Spiel vollständig fixiert!";
                    var body = $"Hallo {p.Firstname},<br><br>" +
                               $"das Börsen-Spiel am <strong>{slot.Date:dd.MM.yyyy}</strong> um {slot.StartTime:hh\\:mm} Uhr ist nun vollständig besetzt!<br><br>" +
                               $"<strong>Teilnehmer:</strong> {playerNames}<br><br>" +
                               $"Vergesst nicht, euch rechtzeitig einen Platz im Hallenplan zu reservieren.<br><br>" +
                               $"Viel Spaß beim Spielen!<br>Dein TennisBruck-Team";

                    _ = emailSender.SendEmailAsync(p.IdentityUser.Email, subject, body);
                }

                if (pushService != null)
                {
                    await pushService.SendNotificationAsync(p.Id, "🎾 Börsen-Spiel fixiert!", $"Dein Spiel am {slot.Date:dd.MM.yyyy} ist komplett besetzt!", "/PartnerBoard");
                }
            }
        }

        return (true, message);
    }

    public async Task<(bool Success, string Message)> CreateSlotAsync(
        int playerId,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        string message,
        int neededPlayers)
    {
        if (startTime >= endTime)
            return (false, "Die Startzeit muss zwingend vor der Endzeit liegen.");

        var newSlot = new AvailabilitySlot
        {
            PlayerId = playerId,
            Date = date,
            StartTime = startTime,
            EndTime = endTime,
            Message = message,
            NeededPlayers = neededPlayers,
            IsDouble = neededPlayers > 1,
            IsMatched = false
        };

        db.AvailabilitySlots.Add(newSlot);
        await db.SaveChangesAsync();

        return (true, "Dein Eintrag wurde erfolgreich veröffentlicht.");
    }

    public async Task<(bool Success, string Message)> EditSlotAsync(
        int slotId,
        int playerId,
        DateTime editDate,
        TimeSpan editStartTime,
        TimeSpan editEndTime,
        string editMessage,
        int editNeededPlayers)
    {
        if (editStartTime >= editEndTime)
            return (false, "Die Startzeit muss zwingend vor der Endzeit liegen.");

        var slot = await db.AvailabilitySlots
            .Include(s => s.Player).ThenInclude(p => p.IdentityUser)
            .Include(s => s.MatchedWithPlayer).ThenInclude(p => p.IdentityUser)
            .Include(s => s.MatchedWithPlayer).ThenInclude(p => p.NotificationSettings)
            .Include(s => s.MatchedWithPlayer2).ThenInclude(p => p.IdentityUser)
            .Include(s => s.MatchedWithPlayer2).ThenInclude(p => p.NotificationSettings)
            .Include(s => s.MatchedWithPlayer3).ThenInclude(p => p.IdentityUser)
            .Include(s => s.MatchedWithPlayer3).ThenInclude(p => p.NotificationSettings)
            .FirstOrDefaultAsync(s => s.Id == slotId);

        if (slot == null)
            return (false, "Dieser Eintrag existiert nicht mehr.");

        if (slot.PlayerId != playerId)
            return (false, "Zugriff verweigert.");

        var oldDate = slot.Date;
        var oldStartTime = slot.StartTime;
        var oldEndTime = slot.EndTime;
        var oldMessage = slot.Message;
        var oldNeededPlayers = slot.NeededPlayers;

        slot.Date = editDate;
        slot.StartTime = editStartTime;
        slot.EndTime = editEndTime;
        slot.Message = editMessage;
        slot.NeededPlayers = editNeededPlayers;
        slot.IsDouble = editNeededPlayers > 1;

        await db.SaveChangesAsync();

        bool isEdited = oldDate != editDate ||
                        oldStartTime != editStartTime ||
                        oldEndTime != editEndTime ||
                        oldMessage != editMessage ||
                        oldNeededPlayers != editNeededPlayers;

        if (isEdited)
        {
            var joinedPlayers = new List<Player>();
            if (slot.MatchedWithPlayer != null) joinedPlayers.Add(slot.MatchedWithPlayer);
            if (slot.MatchedWithPlayer2 != null) joinedPlayers.Add(slot.MatchedWithPlayer2);
            if (slot.MatchedWithPlayer3 != null) joinedPlayers.Add(slot.MatchedWithPlayer3);

            foreach (var p in joinedPlayers)
            {
                if (p.IdentityUser?.Email != null &&
                    (p.NotificationSettings == null || p.NotificationSettings.EmailOnSlotCancelled))
                {
                    var subject = "🎾 Update zu deinem Börsen-Spiel!";
                    var body = $"Hallo {p.Firstname},<br><br>" +
                               $"der Ersteller <strong>{slot.Player?.Firstname} {slot.Player?.Lastname}</strong> hat den Eintrag für euer Börsen-Spiel geändert!<br><br>" +
                               $"<strong>Neue Spieldetails:</strong><br>" +
                               $"- Datum: {editDate:dd.MM.yyyy}<br>" +
                               $"- Uhrzeit: {editStartTime:hh\\:mm} - {editEndTime:hh\\:mm} Uhr<br>" +
                               $"- Typ: {(slot.IsDouble ? "Doppel" : "Einzel")}<br>";

                    if (!string.IsNullOrEmpty(editMessage))
                    {
                        body += $"- Nachricht: \"{editMessage}\"<br>";
                    }

                    body += $"<br>Bitte prüfe im Partnerboard, ob dir die Änderungen passen.<br><br>" +
                            $"Dein TennisBruck-Team";

                    _ = emailSender.SendEmailAsync(p.IdentityUser.Email, subject, body);
                }

                if (pushService != null)
                {
                    await pushService.SendNotificationAsync(p.Id, "🎾 Update Börsen-Spiel", $"Der Termin am {editDate:dd.MM.yyyy} wurde geändert.", "/PartnerBoard");
                }
            }
        }

        return (true, "Dein Eintrag wurde erfolgreich aktualisiert.");
    }

    public async Task<(bool Success, string Message)> DeleteOrLeaveSlotAsync(int slotId, int playerId)
    {
        var slot = await db.AvailabilitySlots
            .Include(s => s.Player).ThenInclude(p => p.IdentityUser)
            .Include(s => s.Player).ThenInclude(p => p.NotificationSettings)
            .Include(s => s.MatchedWithPlayer).ThenInclude(p => p.IdentityUser)
            .Include(s => s.MatchedWithPlayer).ThenInclude(p => p.NotificationSettings)
            .Include(s => s.MatchedWithPlayer2).ThenInclude(p => p.IdentityUser)
            .Include(s => s.MatchedWithPlayer2).ThenInclude(p => p.NotificationSettings)
            .Include(s => s.MatchedWithPlayer3).ThenInclude(p => p.IdentityUser)
            .Include(s => s.MatchedWithPlayer3).ThenInclude(p => p.NotificationSettings)
            .FirstOrDefaultAsync(s => s.Id == slotId);

        if (slot == null)
            return (false, "Dieser Eintrag existiert leider nicht mehr.");

        var player = await db.Players.FindAsync(playerId);

        if (slot.PlayerId == playerId)
        {
            var joinedPlayers = new List<Player>();
            if (slot.MatchedWithPlayer != null) joinedPlayers.Add(slot.MatchedWithPlayer);
            if (slot.MatchedWithPlayer2 != null) joinedPlayers.Add(slot.MatchedWithPlayer2);
            if (slot.MatchedWithPlayer3 != null) joinedPlayers.Add(slot.MatchedWithPlayer3);

            foreach (var p in joinedPlayers)
            {
                if (p.IdentityUser?.Email != null &&
                    p.NotificationSettings?.EmailOnSlotCancelled == true)
                {
                    var subject = "🎾 Börsen-Spiel abgesagt!";
                    var body = $"Hallo {p.Firstname},<br><br>" +
                               $"der Ersteller <strong>{slot.Player?.Firstname} {slot.Player?.Lastname}</strong> hat das Börsen-Spiel am <strong>{slot.Date:dd.MM.yyyy}</strong> um {slot.StartTime:hh\\:mm} Uhr abgesagt.<br><br>" +
                               $"Du wurdest automatisch ausgetragen.<br><br>" +
                               $"Dein TennisBruck-Team";
                    _ = emailSender.SendEmailAsync(p.IdentityUser.Email, subject, body);
                }

                if (pushService != null)
                {
                    await pushService.SendNotificationAsync(p.Id, "🎾 Börsen-Spiel abgesagt", $"Das Spiel am {slot.Date:dd.MM.yyyy} wurde vom Ersteller abgesagt.", "/PartnerBoard");
                }
            }

            db.AvailabilitySlots.Remove(slot);
            await db.SaveChangesAsync();
            return (true, "Dein Eintrag wurde erfolgreich gelöscht.");
        }
        else
        {
            if (slot.MatchedWithPlayerId == playerId) slot.MatchedWithPlayerId = null;
            if (slot.MatchedWithPlayer2Id == playerId) slot.MatchedWithPlayer2Id = null;
            if (slot.MatchedWithPlayer3Id == playerId) slot.MatchedWithPlayer3Id = null;

            slot.IsMatched = false;

            if (slot.Player.IdentityUser?.Email != null && slot.Player.NotificationSettings?.EmailOnSlotCancelled == true)
            {
                var emailSubject = "🎾 Ein Mitspieler hat abgesagt!";
                var emailBody = $"Hallo {slot.Player.Firstname},<br><br>" +
                                $"<strong>{player?.Firstname} {player?.Lastname}</strong> hat sich gerade aus deinem Börsen-Eintrag am <strong>{slot.Date:dd.MM.yyyy}</strong> um {slot.StartTime:hh\\:mm} Uhr ausgetragen.<br>" +
                                "Dein TennisBruck-Team";

                await emailSender.SendEmailAsync(slot.Player.IdentityUser.Email, emailSubject, emailBody);
            }

            if (pushService != null && slot.Player != null)
            {
                await pushService.SendNotificationAsync(slot.Player.Id, "🎾 Ein Mitspieler hat abgesagt", $"{player?.Firstname} {player?.Lastname} hat sich aus deinem Eintrag am {slot.Date:dd.MM.yyyy} ausgetragen.", "/PartnerBoard");
            }

            await db.SaveChangesAsync();
            return (true, "Du hast dich aus dem Match ausgetragen. Der freie Platz wurde wieder in die Börse gestellt.");
        }
    }
}
