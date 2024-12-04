using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TennisBruck.Services;
using TennisDb;

namespace TennisBruck.Pages;

public class CourtBruck : PageModel
{
    private readonly TennisContext _db;
    private readonly CurrentPlayerService _currentPlayerService;
    public DateTime CurrentDate { get; set; } = DateTime.Today;
    public List<(DateTime Time, bool IsBooked)> TimeSlots { get; set; } = new();
    public List<Reservation> Reservations { get; set; } = new();
    public Player? CurrentPlayer { get; private set; }
    [BindProperty] public int CourtNumber { get; set; }
    [BindProperty] public DateTime StartTime { get; set; }
    [BindProperty] public int ReservationId { get; set; }

    public CourtBruck(TennisContext db, CurrentPlayerService currentPlayerService)
    {
        _db = db;
        _currentPlayerService = currentPlayerService;
    }

    public IActionResult OnGet(string? date)
    {
        // Parse the date or default to today
        CurrentDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);

        // Load the logged-in user
        CurrentPlayer = _currentPlayerService.GetCurrentUser(HttpContext.User.Identity?.Name);
        if (CurrentPlayer == null) return RedirectToPage(nameof(Login));

        // Load reservations for the selected day
        Reservations = _db.Reservations
            .Include(r => r.Player)
            .Where(r => r.StartTime.Date == CurrentDate.Date)
            .ToList();

        // Generate half-hour time slots for the day
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


    public IActionResult OnPostCreateReservation()
    {
        CurrentPlayer = _currentPlayerService.GetCurrentUser(HttpContext.User.Identity?.Name);
        if (CurrentPlayer == null) return RedirectToPage(nameof(Login));

        // Check if the reservation already exists
        var existing = _db.Reservations.FirstOrDefault(r =>
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

        _db.Reservations.Add(newReservation);
        _db.SaveChanges();

        return RedirectToPage(new { date = StartTime.ToString("yyyy-MM-dd") });
    }

    public IActionResult OnPostDeleteReservation()
    {
        CurrentPlayer = _currentPlayerService.GetCurrentUser(HttpContext.User.Identity?.Name);
        if (CurrentPlayer == null) return RedirectToPage(nameof(Login));

        Console.WriteLine(StartTime);
        // Find the reservation
        var reservation = _db.Reservations.Include(reservation => reservation.Player)
            .FirstOrDefault(x => x.Id == ReservationId);

        if (reservation == null || reservation.Player.Id != CurrentPlayer.Id)
        {
            ModelState.AddModelError("", "Reservierung nicht gefunden oder Zugriff verweigert.");
            return RedirectToPage(new { date = CurrentDate.ToString("yyyy-MM-dd") });
        }

        _db.Reservations.Remove(reservation);
        _db.SaveChanges();

        return RedirectToPage(new { date = reservation.StartTime.ToString("yyyy-MM-dd") });
    }
}