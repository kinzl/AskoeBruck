using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TennisBruck.Services;
using TennisDb;

namespace TennisBruck.Pages;

public class Punch : PageModel
{
    private readonly TennisContext _db;
    private readonly PlayerService _playerService;

    public Punch(TennisContext db, PlayerService playerService)
    {
        _db = db;
        _playerService = playerService;
        MatchDays = new List<DateTime>();
        WorkingDates = new List<DateTime>();
    }

    [BindProperty] public DateTime StartDate { get; set; }
    [BindProperty] public DateTime EndDate { get; set; }
    public List<DateTime> MatchDays { get; set; }
    public List<DateTime> WorkingDates { get; set; }

    public void OnGet()
    {
        // LoadExistingDates();
    }

    public IActionResult OnPostGenerateDates()
    {
        MatchDays.Clear();
        for (var date = StartDate; date <= EndDate; date = date.AddDays(7))
        {
            if (date.DayOfWeek == DayOfWeek.Friday)
                MatchDays.Add(date);
        }

        // Save generated dates if needed
        SaveDates(MatchDays);

        // LoadExistingDates();
        return Page();
    }

    // public IActionResult OnPostToggleWork(DateTime matchDay)
    // {
    //     var playerId = _playerService.GetCurrentPlayerId(User);
    //     var assignment = _db.WorkAssignments.FirstOrDefault(x => x.PlayerId == playerId && x.WorkDate == matchDay);
    //
    //     if (assignment != null)
    //     {
    //         // Remove assignment if it exists
    //         _db.WorkAssignments.Remove(assignment);
    //     }
    //     else
    //     {
    //         // Add new assignment if not yet assigned
    //         _db.WorkAssignments.Add(new WorkAssignment
    //         {
    //             PlayerId = playerId,
    //             WorkDate = matchDay
    //         });
    //     }
    //
    //     _db.SaveChanges();
    //     LoadExistingDates();
    //
    //     return Page();
    // }
    //
    // private void LoadExistingDates()
    // {
    //     // Load match days and current assignments from the database
    //     MatchDays = _db.Court.Select(c => c.MatchDay).ToList();
    //     var playerId = _playerService.GetCurrentPlayerId(User);
    //     WorkingDates = _db.WorkAssignments.Where(x => x.PlayerId == playerId)
    //         .Select(x => x.WorkDate)
    //         .ToList();
    // }

    private void SaveDates(List<DateTime> dates)
    {
        foreach (var date in dates)
        {
            if (!_db.Court.Any(c => c.MatchDay == date))
            {
                _db.Court.Add(new Court { MatchDay = date });
            }
        }

        _db.SaveChanges();
    }
}
    