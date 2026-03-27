using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages;

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

    public async Task<IActionResult> OnGet(string? date)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser()!;

        // Parse the date or default to today
        CurrentDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);

        var oldSlots = await db.Reservations
            .Where(s => s.StartTime.Date < DateTime.Today)
            .ToListAsync();

        if (oldSlots.Any())
        {
            db.Reservations.RemoveRange(oldSlots);
            await db.SaveChangesAsync();
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

    // Wir fügen string? eventName als Parameter hinzu. ASP.NET fängt das automatisch aus dem HTML (name="eventName") ab!
    public IActionResult OnPostCreateReservation(string? eventName)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser()!;

        // Check if the reservation already exists
        var existing = db.Reservations.FirstOrDefault(r =>
            r.CourtNumber == CourtNumber && r.StartTime == StartTime);

        if (existing != null)
        {
            ModelState.AddModelError("", "Dieser Zeitraum ist bereits reserviert.");
            return RedirectToPage(new { date = StartTime.ToString("yyyy-MM-dd") });
        }

        // Add a new reservation
        var newReservation = new Reservation
        {
            CourtNumber = CourtNumber,
            StartTime = StartTime,
            EndTime = StartTime.AddMinutes(30),
            Player = CurrentPlayer!
        };

        // Wir prüfen: Wurde ein Text eingegeben UND ist der eingeloggte User wirklich ein Admin?
        if (!string.IsNullOrWhiteSpace(eventName) && User.IsInRole("Admin"))
        {
            newReservation.EventName =
                eventName.Trim(); // .Trim() entfernt aus Versehen getippte Leerzeichen am Anfang/Ende
        }

        db.Reservations.Add(newReservation);
        db.SaveChanges();

        return RedirectToPage(new { date = StartTime.ToString("yyyy-MM-dd") });
    }

    public IActionResult OnPostCreateEvent(int CourtNumber, string EventName, string StartTimeStr, string EndTimeStr,
        string CurrentDateStr)
    {
        // Sicherheitscheck
        if (!User.IsInRole("Admin")) return RedirectToPage();

        CurrentPlayer = currentPlayerService.GetCurrentUser()!;

        // Datum und Zeiten aus den Strings parsen
        var date = DateTime.Parse(CurrentDateStr);
        var startTime = DateTime.Parse(StartTimeStr).TimeOfDay;
        var endTime = DateTime.Parse(EndTimeStr).TimeOfDay;

        // Genaue DateTime Objekte für Start und Ende bauen
        var startDateTime = date.Add(startTime);
        var endDateTime = date.Add(endTime);

        // WICHTIG: Schleife, die alle 30 Minuten durchgeht, bis die Endzeit erreicht ist
        for (var time = startDateTime; time < endDateTime; time = time.AddMinutes(30))
        {
            // Prüfen, ob dieser spezifische 30-Min-Block schon belegt ist
            var existing = db.Reservations.FirstOrDefault(r => r.CourtNumber == CourtNumber && r.StartTime == time);

            // Wenn er frei ist, legen wir die Reservierung an
            if (existing == null)
            {
                var newReservation = new Reservation
                {
                    CourtNumber = CourtNumber,
                    StartTime = time,
                    EndTime = time.AddMinutes(30),
                    Player = CurrentPlayer,
                    EventName = EventName.Trim() // Hier setzen wir das Event für jeden Block!
                };

                db.Reservations.Add(newReservation);
            }
        }

        // Alles auf einmal in der Datenbank speichern
        db.SaveChanges();

        return RedirectToPage(new { date = CurrentDateStr });
    }

    public IActionResult OnPostDeleteReservation()
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser()!;

        Console.WriteLine(StartTime);
        // Find the reservation
        var reservation = db.Reservations.Include(reservation => reservation.Player)
            .FirstOrDefault(x => x.Id == ReservationId);

        if (reservation?.Player != null && reservation.Player.Id != CurrentPlayer.Id)
        {
            ModelState.AddModelError("", "Reservierung nicht gefunden oder Zugriff verweigert.");
            return RedirectToPage(new { date = CurrentDate.ToString("yyyy-MM-dd") });
        }

        db.Reservations.Remove(reservation);
        db.SaveChanges();

        return RedirectToPage(new { date = reservation.StartTime.ToString("yyyy-MM-dd") });
    }
}