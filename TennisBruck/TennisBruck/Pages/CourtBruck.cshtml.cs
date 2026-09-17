namespace TennisBruck.Pages;

public class CourtBruck(TennisContext db, CurrentPlayerService currentPlayerService, ReservationService reservationService)
    : PageModel
{
    public DateTime CurrentDate { get; set; } = CityTime.GetViennaTimeZone();
    public List<(DateTime Time, bool IsBooked)> TimeSlots { get; set; } = [];
    public List<Reservation> Reservations { get; set; } = [];
    public List<Player> AllPlayers { get; set; } = [];
    public Player? CurrentPlayer { get; private set; }
    [BindProperty] public int ReservationId { get; set; }
    [BindProperty] public string? Message { get; set; }
    [BindProperty] public bool IsError { get; set; }

    public Dictionary<(int CourtNumber, DateTime StartTime), TennisBruck.Features.Reservations.ReservationBlock> BlockInfo { get; set; } = [];

    public IActionResult OnGet(string? date, string? message, bool isError = false)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        Message = message;
        IsError = isError;
        CurrentDate = string.IsNullOrEmpty(date) ? CityTime.GetViennaTimeZone() : DateTime.Parse(date);

        AllPlayers = db.Players.OrderBy(p => p.Lastname).ThenBy(p => p.Firstname).ToList();

        var overview = reservationService.GetCourtOverview(CurrentDate);
        Reservations = overview.Reservations;
        TimeSlots = overview.TimeSlots;
        BlockInfo = overview.BlockInfo;

        return Page();
    }

    public Reservation? GetReservation(int courtNumber, DateTime startTime)
    {
        return Reservations.FirstOrDefault(r => r.CourtNumber == courtNumber && r.StartTime == startTime);
    }

    public IActionResult OnPostCreateReservation(int courtNumber, string startTimeStr, string? endTimeStr,
        int? partnerId, string? eventName, string currentDateStr)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        if (CurrentPlayer == null)
        {
            return RedirectToPage(new { date = currentDateStr, message = "Bitte melde dich an.", isError = true });
        }

        var date = DateTime.Parse(currentDateStr);
        var startTime = DateTime.Parse(startTimeStr).TimeOfDay;
        TimeSpan? endTime = !string.IsNullOrEmpty(endTimeStr) ? DateTime.Parse(endTimeStr).TimeOfDay : null;

        var (success, message) = reservationService.CreateReservation(
            courtNumber, date, startTime, endTime, CurrentPlayer.Id, partnerId, eventName);

        return RedirectToPage(new { date = currentDateStr, message, isError = !success });
    }

    public IActionResult OnPostCreateEvent(int courtNumber, string? eventName, string startTimeStr, string endTimeStr,
        string currentDateStr)
    {
        return OnPostCreateReservation(courtNumber, startTimeStr, endTimeStr, null, eventName, currentDateStr);
    }

    public IActionResult OnPostUpdateReservation(int reservationId, int courtNumber, string startTimeStr,
        string endTimeStr, int? partnerId, string? eventName, string currentDateStr)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        if (CurrentPlayer == null)
        {
            return RedirectToPage(new { date = currentDateStr, message = "Bitte melde dich an.", isError = true });
        }

        var targetRes = db.Reservations
            .Include(r => r.Player)
            .FirstOrDefault(x => x.Id == reservationId);

        if (targetRes == null)
        {
            return RedirectToPage(new
                { date = currentDateStr, message = "Reservierung nicht gefunden.", isError = true });
        }

        if (!User.IsInRole("Admin") && targetRes.Player?.Id != CurrentPlayer.Id)
        {
            return RedirectToPage(new { date = currentDateStr, message = "Zugriff verweigert.", isError = true });
        }

        var allDayRes = db.Reservations
            .Where(r => r.CourtNumber == targetRes.CourtNumber &&
                        r.StartTime.Date == targetRes.StartTime.Date &&
                        r.Player.Id == targetRes.Player.Id &&
                        r.PartnerId == targetRes.PartnerId &&
                        r.EventName == targetRes.EventName)
            .ToList();

        var existingBlock = GetContiguousBlock(targetRes, allDayRes);
        var existingBlockIds = existingBlock.Select(r => r.Id).ToHashSet();

        var date = DateTime.Parse(currentDateStr);
        var startTime = DateTime.Parse(startTimeStr).TimeOfDay;
        var endTime = DateTime.Parse(endTimeStr).TimeOfDay;

        var startDateTime = date.Add(startTime);
        var endDateTime = date.Add(endTime);

        if (endDateTime <= startDateTime)
        {
            return RedirectToPage(new
                { date = currentDateStr, message = "Die Endzeit muss nach der Startzeit liegen.", isError = true });
        }

        var hasConflict = db.Reservations.Any(r =>
            r.CourtNumber == courtNumber &&
            !existingBlockIds.Contains(r.Id) &&
            r.StartTime >= startDateTime &&
            r.StartTime < endDateTime);

        if (hasConflict)
        {
            return RedirectToPage(new
            {
                date = currentDateStr,
                message = "Der geänderte Zeitraum überschneidet sich mit einer anderen Reservierung.", isError = true
            });
        }

        db.Reservations.RemoveRange(existingBlock);

        Player? partner = null;
        if (partnerId.HasValue && partnerId.Value > 0 && partnerId.Value != targetRes.Player?.Id)
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
                Player = targetRes.Player,
                PartnerId = partner?.Id,
                EventName = string.IsNullOrWhiteSpace(eventName) ? null : eventName.Trim()
            };
            db.Reservations.Add(newReservation);
        }

        db.SaveChanges();

        return RedirectToPage(new { date = currentDateStr, message = "Reservierung wurde erfolgreich aktualisiert!" });
    }

    public IActionResult OnPostDeleteReservation()
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        if (CurrentPlayer == null)
        {
            return RedirectToPage(new
                { date = CurrentDate.ToString("yyyy-MM-dd"), message = "Bitte melde dich an.", isError = true });
        }

        var (success, message) = reservationService.DeleteReservation(
            ReservationId, CurrentPlayer.Id, User.IsInRole("Admin"));

        return RedirectToPage(new
        {
            date = CurrentDate.ToString("yyyy-MM-dd"),
            message,
            isError = !success
        });
    }

    public async Task<IActionResult> OnPostDeleteSelectedReservationsAsync(List<int> selectedReservationIds)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();

        if (CurrentPlayer == null || !User.IsInRole("Admin"))
        {
            return RedirectToPage(new
                { date = CurrentDate.ToString("yyyy-MM-dd"), message = "Zugriff verweigert.", isError = true });
        }

        if (!selectedReservationIds.Any())
        {
            return RedirectToPage(new
                { date = CurrentDate.ToString("yyyy-MM-dd"), message = "Keine Reservierungen ausgewählt." });
        }

        var selectedReservations = await db.Reservations
            .Include(r => r.Player)
            .Where(r => selectedReservationIds.Contains(r.Id))
            .ToListAsync();

        var allReservationsToDelete = new HashSet<Reservation>();

        foreach (var reservation in selectedReservations)
        {
            var allDayRes = await db.Reservations
                .Where(r => r.CourtNumber == reservation.CourtNumber &&
                            r.StartTime.Date == reservation.StartTime.Date &&
                            r.Player.Id == reservation.Player.Id &&
                            r.PartnerId == reservation.PartnerId &&
                            r.EventName == reservation.EventName)
                .ToListAsync();

            foreach (var r in GetContiguousBlock(reservation, allDayRes))
            {
                allReservationsToDelete.Add(r);
            }
        }

        db.Reservations.RemoveRange(allReservationsToDelete);
        await db.SaveChangesAsync();

        return RedirectToPage(new
        {
            date = CurrentDate.ToString("yyyy-MM-dd"),
            message = "Die ausgewählten Reservierungen wurden erfolgreich gelöscht."
        });
    }

    private static List<Reservation> GetContiguousBlock(Reservation reservation, List<Reservation> allDayRes)
    {
        var block = new List<Reservation> { reservation };

        for (var t = reservation.StartTime.AddMinutes(-30);; t = t.AddMinutes(-30))
        {
            var prev = allDayRes.FirstOrDefault(r => r.StartTime == t);
            if (prev == null) break;
            block.Add(prev);
        }

        for (var t = reservation.StartTime.AddMinutes(30);; t = t.AddMinutes(30))
        {
            var next = allDayRes.FirstOrDefault(r => r.StartTime == t);
            if (next == null) break;
            block.Add(next);
        }

        return block;
    }
}