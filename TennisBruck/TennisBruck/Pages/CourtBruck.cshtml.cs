namespace TennisBruck.Pages;

public class CourtBruck(TennisContext db, CurrentPlayerService currentPlayerService)
    : PageModel
{
    public DateTime CurrentDate { get; set; } = DateTime.Today;
    public List<(DateTime Time, bool IsBooked)> TimeSlots { get; set; } = new();
    public List<Reservation> Reservations { get; set; } = new();
    public Player? CurrentPlayer { get; private set; }
    [BindProperty] public int CourtNumber { get; set; }
    [BindProperty] public DateTime StartTime { get; set; }
    [BindProperty] public int ReservationId { get; set; }
    [BindProperty] public string? Message { get; set; }
    [BindProperty] public bool IsError { get; set; }

    public async Task<IActionResult> OnGet(string? date, string? message, bool isError = false)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser()!;
        Message = message;
        IsError = isError;
        // Parse the date or default to today
        CurrentDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);

        if (CurrentDate.Date < DateTime.Today)
        {
            CurrentDate = DateTime.Today;
        }

        Reservations = db.Reservations
            .Include(r => r.Player)
            .Where(r => r.StartTime.Date == CurrentDate.Date)
            .ToList();

        var start = CurrentDate.AddHours(8); // Start at 8 AM
        var end = CurrentDate.AddHours(22); // End at 10 PM
        while (start < end)
        {
            TimeSlots.Add((start, Reservations.Any(r => r.StartTime == start)));
            start = start.AddMinutes(30);
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
            return RedirectToPage(new { date = StartTime.ToString("yyyy-MM-dd"), message = "Bitte melde dich an.", isError = true });
        }

        if (StartTime < DateTime.Now)
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

        // Datum und Zeiten aus den Strings parsen
        var date = DateTime.Parse(currentDateStr);
        var startTime = DateTime.Parse(startTimeStr).TimeOfDay;
        var endTime = DateTime.Parse(endTimeStr).TimeOfDay;

        // Genaue DateTime Objekte für Start und Ende bauen
        var startDateTime = date.Add(startTime);
        var endDateTime = date.Add(endTime);

        if (startDateTime < DateTime.Now)
        {
            return RedirectToPage(new
            {
                date = currentDateStr,
                message = "Reservierungen in der Vergangenheit sind nicht erlaubt.",
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

        Console.WriteLine(StartTime);
        // Find the reservation
        var reservation = db.Reservations.Include(reservation => reservation.Player)
            .FirstOrDefault(x => x.Id == ReservationId);

        if (reservation?.Player != null && reservation.Player.Id != CurrentPlayer.Id)
        {
            return RedirectToPage(new
            {
                date = CurrentDate.ToString("yyyy-MM-dd"),
                message = "Reservierung nicht gefunden oder Zugriff verweigert.", isError = true
            });
        }

        db.Reservations.Remove(reservation);
        db.SaveChanges();

        return RedirectToPage(new
        {
            date = reservation.StartTime.ToString("yyyy-MM-dd"), message = "Reservierung wurde erfolgreich gelöscht."
        });
    }
}