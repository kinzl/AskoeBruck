using Microsoft.EntityFrameworkCore;
using TennisDb;

namespace TennisBruck.Features.Reservations;

public class ReservationBlock
{
    public required Reservation Reservation { get; set; }
    public int RowSpan { get; set; }
    public bool IsStart { get; set; }
}

public class CourtOverview
{
    public DateTime CurrentDate { get; set; }
    public List<(DateTime Time, bool IsBooked)> TimeSlots { get; set; } = [];
    public List<Reservation> Reservations { get; set; } = [];
    public Dictionary<(int CourtNumber, DateTime StartTime), ReservationBlock> BlockInfo { get; set; } = [];
}

public class ReservationService(TennisContext db)
{
    public CourtOverview GetCourtOverview(DateTime date)
    {
        var reservations = db.Reservations
            .Include(r => r.Player)
            .Include(r => r.Partner)
            .Where(r => r.StartTime.Date == date.Date)
            .OrderBy(r => r.CourtNumber)
            .ThenBy(r => r.StartTime)
            .ToList();

        var timeSlots = new List<(DateTime Time, bool IsBooked)>();
        var start = date.Date.AddHours(8);
        var end = date.Date.AddHours(22);
        while (start < end)
        {
            timeSlots.Add((start, reservations.Any(r => r.StartTime == start)));
            start = start.AddMinutes(30);
        }

        var blockInfo = CalculateBlockInfo(reservations);

        return new CourtOverview
        {
            CurrentDate = date,
            Reservations = reservations,
            TimeSlots = timeSlots,
            BlockInfo = blockInfo
        };
    }

    public Dictionary<(int CourtNumber, DateTime StartTime), ReservationBlock> CalculateBlockInfo(List<Reservation> reservations)
    {
        var blockInfo = new Dictionary<(int CourtNumber, DateTime StartTime), ReservationBlock>();

        for (int court = 1; court <= 3; court++)
        {
            var courtReservations = reservations.Where(r => r.CourtNumber == court).OrderBy(r => r.StartTime).ToList();
            int i = 0;
            while (i < courtReservations.Count)
            {
                var startRes = courtReservations[i];
                int rowSpan = 1;
                int j = i + 1;

                while (j < courtReservations.Count)
                {
                    var currentRes = courtReservations[j];
                    var prevRes = courtReservations[j - 1];

                    bool isContiguous = currentRes.StartTime == prevRes.EndTime;
                    bool isSamePlayer = currentRes.Player?.Id == startRes.Player?.Id;
                    bool isSamePartner = currentRes.PartnerId == startRes.PartnerId;
                    bool isSameEvent = currentRes.EventName == startRes.EventName;

                    if (isContiguous && isSamePlayer && isSamePartner && isSameEvent)
                    {
                        rowSpan++;
                        j++;
                    }
                    else
                    {
                        break;
                    }
                }

                blockInfo[(court, startRes.StartTime)] = new ReservationBlock
                {
                    Reservation = startRes,
                    RowSpan = rowSpan,
                    IsStart = true
                };

                for (int k = i + 1; k < j; k++)
                {
                    blockInfo[(court, courtReservations[k].StartTime)] = new ReservationBlock
                    {
                        Reservation = courtReservations[k],
                        RowSpan = 0,
                        IsStart = false
                    };
                }

                i = j;
            }
        }

        return blockInfo;
    }

    public (bool Success, string Message) CreateReservation(
        int courtNumber,
        DateTime date,
        TimeSpan startTime,
        TimeSpan? endTime,
        int currentPlayerId,
        int? partnerId,
        string? eventName)
    {
        var player = db.Players.FirstOrDefault(p => p.Id == currentPlayerId);
        if (player == null) return (false, "Spieler nicht gefunden.");

        var startDateTime = date.Date.Add(startTime);
        DateTime endDateTime = endTime.HasValue
            ? date.Date.Add(endTime.Value)
            : startDateTime.AddHours(2);

        if (startDateTime < CityTime.GetViennaTimeZone())
        {
            return (false, "Reservierungen in der Vergangenheit sind nicht erlaubt.");
        }

        if (endDateTime <= startDateTime)
        {
            return (false, "Die Endzeit muss nach der Startzeit liegen.");
        }

        var hasConflict = db.Reservations.Any(r =>
            r.CourtNumber == courtNumber &&
            r.StartTime >= startDateTime &&
            r.StartTime < endDateTime);

        if (hasConflict)
        {
            return (false, "Dieser Zeitraum ist bereits teilweise oder vollständig reserviert.");
        }

        Player? partner = null;
        if (partnerId.HasValue && partnerId.Value > 0 && partnerId.Value != currentPlayerId)
        {
            partner = db.Players.FirstOrDefault(p => p.Id == partnerId.Value);
        }

        for (var time = startDateTime; time < endDateTime; time = time.AddMinutes(30))
        {
            var newReservation = new Reservation
            {
                CourtNumber = courtNumber,
                StartTime = time,
                EndTime = time.AddMinutes(30),
                Player = player,
                PartnerId = partner?.Id,
                EventName = string.IsNullOrWhiteSpace(eventName) ? null : eventName.Trim()
            };
            db.Reservations.Add(newReservation);
        }

        db.SaveChanges();
        return (true, "Termin wurde erfolgreich reserviert!");
    }

    public (bool Success, string Message, int BookedCount, int SkippedCount) CreateRecurringReservations(
        int courtNumber,
        DateTime startDate,
        TimeSpan startTime,
        TimeSpan? endTime,
        int currentPlayerId,
        int? partnerId,
        string? eventName,
        int repeatWeeks = 1)
    {
        if (repeatWeeks <= 1)
        {
            var singleResult = CreateReservation(courtNumber, startDate, startTime, endTime, currentPlayerId, partnerId, eventName);
            return (singleResult.Success, singleResult.Message, singleResult.Success ? 1 : 0, singleResult.Success ? 0 : 1);
        }

        if (repeatWeeks > 20)
        {
            repeatWeeks = 20;
        }

        var player = db.Players.FirstOrDefault(p => p.Id == currentPlayerId);
        if (player == null) return (false, "Spieler nicht gefunden.", 0, repeatWeeks);

        Player? partner = null;
        if (partnerId.HasValue && partnerId.Value > 0 && partnerId.Value != currentPlayerId)
        {
            partner = db.Players.FirstOrDefault(p => p.Id == partnerId.Value);
        }

        var conflictDates = new List<string>();
        int bookedWeeks = 0;
        var reservationsToAdd = new List<Reservation>();

        for (int i = 0; i < repeatWeeks; i++)
        {
            var currentDate = startDate.Date.AddDays(7 * i);
            var startDateTime = currentDate.Add(startTime);
            DateTime endDateTime = endTime.HasValue
                ? currentDate.Add(endTime.Value)
                : startDateTime.AddHours(2);

            if (startDateTime < CityTime.GetViennaTimeZone())
            {
                conflictDates.Add($"{currentDate:dd.MM.} (Vergangenheit)");
                continue;
            }

            if (endDateTime <= startDateTime)
            {
                return (false, "Die Endzeit muss nach der Startzeit liegen.", 0, repeatWeeks);
            }

            bool hasConflict = db.Reservations.Any(r =>
                r.CourtNumber == courtNumber &&
                r.StartTime >= startDateTime &&
                r.StartTime < endDateTime);

            if (hasConflict)
            {
                conflictDates.Add(currentDate.ToString("dd.MM."));
                continue;
            }

            for (var time = startDateTime; time < endDateTime; time = time.AddMinutes(30))
            {
                reservationsToAdd.Add(new Reservation
                {
                    CourtNumber = courtNumber,
                    StartTime = time,
                    EndTime = time.AddMinutes(30),
                    Player = player,
                    PartnerId = partner?.Id,
                    EventName = string.IsNullOrWhiteSpace(eventName) ? null : eventName.Trim()
                });
            }

            bookedWeeks++;
        }

        if (bookedWeeks == 0)
        {
            return (false, $"Keiner der {repeatWeeks} Termine konnte gebucht werden, da alle Zeiträume bereits belegt waren.", 0, conflictDates.Count);
        }

        db.Reservations.AddRange(reservationsToAdd);
        db.SaveChanges();

        if (conflictDates.Count == 0)
        {
            return (true, $"Alle {bookedWeeks} wöchentlichen Termine erfolgreich reserviert!", bookedWeeks, 0);
        }

        return (true, $"{bookedWeeks} von {repeatWeeks} Terminen gebucht. Belegt an: {string.Join(", ", conflictDates)}.", bookedWeeks, conflictDates.Count);
    }

    public List<Reservation> GetContiguousBlock(Reservation target, List<Reservation> allReservations)
    {
        var sorted = allReservations.OrderBy(r => r.StartTime).ToList();
        var index = sorted.FindIndex(r => r.Id == target.Id);
        if (index == -1) return [target];

        var block = new List<Reservation> { target };

        for (int i = index - 1; i >= 0; i--)
        {
            if (sorted[i].EndTime == block.First().StartTime)
                block.Insert(0, sorted[i]);
            else
                break;
        }

        for (int i = index + 1; i < sorted.Count; i++)
        {
            if (sorted[i].StartTime == block.Last().EndTime)
                block.Add(sorted[i]);
            else
                break;
        }

        return block;
    }

    public (bool Success, string Message) DeleteReservation(int reservationId, int currentPlayerId, bool isAdmin)
    {
        var targetRes = db.Reservations
            .Include(r => r.Player)
            .FirstOrDefault(x => x.Id == reservationId);

        if (targetRes == null)
            return (false, "Reservierung nicht gefunden.");

        if (!isAdmin && targetRes.Player?.Id != currentPlayerId)
            return (false, "Zugriff verweigert.");

        var allDayRes = db.Reservations
            .Where(r => r.CourtNumber == targetRes.CourtNumber &&
                        r.StartTime.Date == targetRes.StartTime.Date &&
                        r.Player.Id == targetRes.Player.Id &&
                        r.PartnerId == targetRes.PartnerId &&
                        r.EventName == targetRes.EventName)
            .ToList();

        var blockToDelete = GetContiguousBlock(targetRes, allDayRes);
        db.Reservations.RemoveRange(blockToDelete);
        db.SaveChanges();

        return (true, "Reservierung wurde erfolgreich gelöscht.");
    }
}
