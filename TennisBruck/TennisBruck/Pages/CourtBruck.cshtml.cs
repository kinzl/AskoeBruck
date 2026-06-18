namespace TennisBruck.Pages;

public class CourtBruck(TennisContext db, CurrentPlayerService currentPlayerService)
    : PageModel
{
    public DateTime CurrentDate { get; set; } = CityTime.GetViennaTimeZone();
    public List<(DateTime Time, bool IsBooked)> TimeSlots { get; set; } = [];
    public List<Reservation> Reservations { get; set; } = [];
    public Player? CurrentPlayer { get; private set; }
    [BindProperty] public int CourtNumber { get; set; }
    [BindProperty] public DateTime StartTime { get; set; }
    [BindProperty] public int ReservationId { get; set; }
    [BindProperty] public string? Message { get; set; }
    [BindProperty] public bool IsError { get; set; }

    public class ReservationBlock
    {
        public required Reservation Reservation { get; set; }
        public int RowSpan { get; set; }
        public bool IsStart { get; set; }
    }

    public Dictionary<(int CourtNumber, DateTime StartTime), ReservationBlock> BlockInfo { get; set; } = [];

    public IActionResult OnGet(string? date, string? message, bool isError = false)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        Message = message;
        IsError = isError;
        CurrentDate = string.IsNullOrEmpty(date) ? CityTime.GetViennaTimeZone() : DateTime.Parse(date);

        Reservations = db.Reservations
            .Include(r => r.Player)
            .Where(r => r.StartTime.Date == CurrentDate.Date)
            .OrderBy(r => r.CourtNumber)
            .ThenBy(r => r.StartTime)
            .ToList();

        var start = CurrentDate.Date.AddHours(8);
        var end = CurrentDate.Date.AddHours(22);
        while (start < end)
        {
            TimeSlots.Add((start, Reservations.Any(r => r.StartTime == start)));
            start = start.AddMinutes(30);
        }

        // Calculate block groupings for rowspan merging
        for (int court = 1; court <= 3; court++)
        {
            var courtReservations = Reservations.Where(r => r.CourtNumber == court).OrderBy(r => r.StartTime).ToList();
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
                    bool isSameEvent = currentRes.EventName == startRes.EventName;
                    
                    if (isContiguous && isSamePlayer && isSameEvent)
                    {
                        rowSpan++;
                        j++;
                    }
                    else
                    {
                        break;
                    }
                }
                
                BlockInfo[(court, startRes.StartTime)] = new ReservationBlock
                {
                    Reservation = startRes,
                    RowSpan = rowSpan,
                    IsStart = true
                };
                
                for (int k = i + 1; k < j; k++)
                {
                    BlockInfo[(court, courtReservations[k].StartTime)] = new ReservationBlock
                    {
                        Reservation = courtReservations[k],
                        RowSpan = 0,
                        IsStart = false
                    };
                }
                
                i = j;
            }
        }

        return Page();
    }

    public Reservation? GetReservation(int courtNumber, DateTime startTime)
    {
        return Reservations.FirstOrDefault(r => r.CourtNumber == courtNumber && r.StartTime == startTime);
    }

    public IActionResult OnPostCreateReservation(string? eventName)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();

        if (CurrentPlayer == null)
        {
            return RedirectToPage(new
                { date = StartTime.ToString("yyyy-MM-dd"), message = "Bitte melde dich an.", isError = true });
        }

        if (StartTime < CityTime.GetViennaTimeZone())
        {
            return RedirectToPage(new
            {
                date = StartTime.ToString("yyyy-MM-dd"),
                message = "Reservierungen in der Vergangenheit sind nicht erlaubt.", isError = true
            });
        }

        // Check if the reservation already exists
        var existing = db.Reservations.FirstOrDefault(r =>
            r.CourtNumber == CourtNumber && r.StartTime == StartTime);

        if (existing != null)
        {
            return RedirectToPage(new
            {
                date = StartTime.ToString("yyyy-MM-dd"),
                message = "Dieser Zeitraum ist bereits reserviert, Termin konnte nicht gebucht werden.", isError = true
            });
        }

        var newReservation = new Reservation
        {
            CourtNumber = CourtNumber,
            StartTime = StartTime,
            EndTime = StartTime.AddMinutes(30),
            Player = CurrentPlayer!
        };

        if (!string.IsNullOrWhiteSpace(eventName) && User.IsInRole("Admin"))
        {
            newReservation.EventName =
                eventName.Trim();
        }

        db.Reservations.Add(newReservation);
        db.SaveChanges();

        return RedirectToPage(new
            { date = StartTime.ToString("yyyy-MM-dd"), message = "Termin wurde erfolgreich reserviert!" });
    }

    public IActionResult OnPostCreateEvent(int courtNumber, string? eventName, string startTimeStr, string endTimeStr,
        string currentDateStr)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        if (CurrentPlayer == null) return RedirectToPage();

        var date = DateTime.Parse(currentDateStr);
        var startTime = DateTime.Parse(startTimeStr).TimeOfDay;
        var endTime = DateTime.Parse(endTimeStr).TimeOfDay;

        var startDateTime = date.Add(startTime);
        var endDateTime = date.Add(endTime);

        if (startDateTime < CityTime.GetViennaTimeZone())
        {
            return RedirectToPage(new
            {
                date = currentDateStr,
                message = "Reservierungen in der Vergangenheit sind nicht erlaubt.",
                isError = true
            });
        }

        if (endDateTime <= startDateTime)
        {
            return RedirectToPage(new
            {
                date = currentDateStr,
                message = "Die Endzeit muss nach der Startzeit liegen.",
                isError = true
            });
        }

        // Prüfen, ob irgendein Slot in dem Zeitraum bereits reserviert ist
        var hasConflict = db.Reservations.Any(r =>
            r.CourtNumber == courtNumber &&
            r.StartTime >= startDateTime &&
            r.StartTime < endDateTime);

        if (hasConflict)
        {
            return RedirectToPage(new
            {
                date = currentDateStr,
                message =
                    "Dieser Zeitraum ist bereits teilweise oder vollständig reserviert. Termin konnte nicht gebucht werden.",
                isError = true
            });
        }

        // WICHTIG: Schleife, die alle 30 Minuten durchgeht, bis die Endzeit erreicht ist
        for (var time = startDateTime; time < endDateTime; time = time.AddMinutes(30))
        {
            var newReservation = new Reservation
            {
                CourtNumber = courtNumber,
                StartTime = time,
                EndTime = time.AddMinutes(30),
                Player = CurrentPlayer,
                EventName = string.IsNullOrWhiteSpace(eventName) ? null : eventName.Trim()
            };

            db.Reservations.Add(newReservation);
        }

        // Alles auf einmal in der Datenbank speichern
        db.SaveChanges();

        return RedirectToPage(new
            { date = currentDateStr, message = "Die Block-Reservierung wurde erfolgreich angelegt!" });
    }

    public IActionResult OnPostDeleteReservation()
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        if (CurrentPlayer == null)
        {
            return RedirectToPage(new { date = CurrentDate.ToString("yyyy-MM-dd"), message = "Bitte melde dich an.", isError = true });
        }

        var reservation = db.Reservations.Include(r => r.Player).FirstOrDefault(x => x.Id == ReservationId);
        if (reservation == null)
        {
            return RedirectToPage(new { date = CurrentDate.ToString("yyyy-MM-dd"), message = "Reservierung nicht gefunden.", isError = true });
        }

        if (!User.IsInRole("Admin") && reservation.Player?.Id != CurrentPlayer.Id)
        {
            return RedirectToPage(new { date = reservation.StartTime.ToString("yyyy-MM-dd"), message = "Zugriff verweigert.", isError = true });
        }

        // Find contiguous block
        var allDayRes = db.Reservations
            .Where(r => r.CourtNumber == reservation.CourtNumber &&
                        r.StartTime.Date == reservation.StartTime.Date &&
                        r.Player.Id == reservation.Player.Id &&
                        r.EventName == reservation.EventName)
            .ToList();

        var blockToDelete = new List<Reservation> { reservation };

        // Check backwards
        var currentTime = reservation.StartTime;
        while (true)
        {
            currentTime = currentTime.AddMinutes(-30);
            var prev = allDayRes.FirstOrDefault(r => r.StartTime == currentTime);
            if (prev != null)
                blockToDelete.Add(prev);
            else
                break;
        }

        // Check forwards
        currentTime = reservation.StartTime;
        while (true)
        {
            currentTime = currentTime.AddMinutes(30);
            var next = allDayRes.FirstOrDefault(r => r.StartTime == currentTime);
            if (next != null)
                blockToDelete.Add(next);
            else
                break;
        }

        db.Reservations.RemoveRange(blockToDelete);
        db.SaveChanges();

        return RedirectToPage(new { date = reservation.StartTime.ToString("yyyy-MM-dd"), message = "Die Reservierung(en) wurde(n) erfolgreich gelöscht." });
    }
    
    public async Task<IActionResult> OnPostDeleteSelectedReservationsAsync(List<int> selectedReservationIds)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();

        if (CurrentPlayer == null || !User.IsInRole("Admin"))
        {
            return RedirectToPage(new
                { date = CurrentDate.ToString("yyyy-MM-dd"), message = "Zugriff verweigert.", isError = true });
        }

        if (selectedReservationIds == null || !selectedReservationIds.Any())
        {
            return RedirectToPage(new
                { date = CurrentDate.ToString("yyyy-MM-dd"), message = "Keine Reservierungen ausgewählt." });
        }

        var selectedReservations = await db.Reservations
            .Include(r => r.Player)
            .Where(r => selectedReservationIds.Contains(r.Id))
            .ToListAsync();

        var allReservationsToDelete = new List<Reservation>();

        foreach (var reservation in selectedReservations)
        {
            var allDayRes = await db.Reservations
                .Where(r => r.CourtNumber == reservation.CourtNumber &&
                            r.StartTime.Date == reservation.StartTime.Date &&
                            r.Player.Id == reservation.Player.Id &&
                            r.EventName == reservation.EventName)
                .ToListAsync();

            if (!allReservationsToDelete.Any(r => r.Id == reservation.Id))
            {
                allReservationsToDelete.Add(reservation);
            }

            // Check backwards
            var currentTime = reservation.StartTime;
            while (true)
            {
                currentTime = currentTime.AddMinutes(-30);
                var prev = allDayRes.FirstOrDefault(r => r.StartTime == currentTime);
                if (prev != null && !allReservationsToDelete.Any(r => r.Id == prev.Id))
                    allReservationsToDelete.Add(prev);
                else
                    break;
            }

            // Check forwards
            currentTime = reservation.StartTime;
            while (true)
            {
                currentTime = currentTime.AddMinutes(30);
                var next = allDayRes.FirstOrDefault(r => r.StartTime == currentTime);
                if (next != null && !allReservationsToDelete.Any(r => r.Id == next.Id))
                    allReservationsToDelete.Add(next);
                else
                    break;
            }
        }

        db.Reservations.RemoveRange(allReservationsToDelete);
        await db.SaveChangesAsync();

        return RedirectToPage(new
            { date = CurrentDate.ToString("yyyy-MM-dd"), message = "Die ausgewählten Reservierungen wurden erfolgreich gelöscht." });
    }
}