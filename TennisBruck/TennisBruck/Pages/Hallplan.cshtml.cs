using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TennisBruck.Services;
using TennisDb;

namespace TennisBruck.Pages;

[Authorize]
public class Hallplan : PageModel
{
    private readonly CurrentPlayerService _currentPlayerService;
    private readonly TennisContext _db;
    private readonly ILogger<Hallplan> _logger;

    public Hallplan(CurrentPlayerService currentPlayerService, TennisContext db,
        ILogger<Hallplan> logger)
    {
        _currentPlayerService = currentPlayerService;
        _db = db;
        _logger = logger;
    }

    public Player LoggedInPlayer { get; set; } = null!;

    [BindProperty] public List<HallPlanDay> HallPlanDays { get; set; } = new();

    public IEnumerable<Player> NotRegisteredPlayers { get; set; } = new List<Player>();
    public IEnumerable<Player> RegisteredPlayers { get; set; } = new List<Player>();
    public required IEnumerable<HallPlanEntity> HallPlanEntity { get; set; }
    [BindProperty] public int HallPlanId { get; set; }
    public bool IsLoggedInPlayerPlaying { get; set; }

    public HallPlanEntity? SelectedHallPlanEntity { get; set; }

    public IActionResult OnPost()
    {
        return InitValues();
    }

    public IActionResult OnGet()
    {
        return InitValues();
    }

    private IActionResult InitValues()
    {
        HallPlanId = HttpContext.Session.GetInt32("selectedHallPlanId") ?? 0;

        LoggedInPlayer = _currentPlayerService.GetCurrentUser(HttpContext.User.Identity!.Name!)!;

        HallPlanEntity = _db.HallPlanEntities.ToList();
        SelectedHallPlanEntity = _db.HallPlanEntities.SingleOrDefault(x => x.Id == HallPlanId);

        HallPlanDays = _db.HallPlanDays
            .Where(x => x.HallPlanId == HallPlanId)
            .Include(x => x.Players)
            .ThenInclude(x => x.Player)
            .OrderBy(x => x.PlayDate)
            .ToList();

        NotRegisteredPlayers = _db.Players
            .Where(p => !_db.HallPlanRegistrations
                .Any(r => r.PlayerId == p.Id && r.HallPlanId == HallPlanId))
            .ToList();

        RegisteredPlayers = _db.Players
            .Where(p => _db.HallPlanRegistrations
                .Any(r => r.PlayerId == p.Id && r.HallPlanId == HallPlanId))
            .ToList();

        foreach (var court in HallPlanDays)
        {
            court.Players = court.Players.OrderBy(p => p.Player.ToString()).ToList();
        }

        if (SelectedHallPlanEntity != null)
        {
            IsLoggedInPlayerPlaying = _db.HallPlanEntities
                .Include(hallPlanEntity => hallPlanEntity.Registrations)
                .Single(x => x.Id == SelectedHallPlanEntity.Id)
                .Registrations.Any(x => x.Player.Id == LoggedInPlayer.Id);
        }

        return Page();
    }

    public IActionResult OnPostCreateCompetition(string? competitionName)
    {
        if (string.IsNullOrWhiteSpace(competitionName))
            return RedirectToPage(nameof(Hallplan));

        var plan = new HallPlanEntity
        {
            Name = competitionName
        };

        _db.HallPlanEntities.Add(plan);
        _db.SaveChanges();

        return RedirectToPage(nameof(Hallplan));
    }

    public IActionResult OnPostGeneratePlan(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Starting plan generation");
        GeneratePlan(startDate, endDate, HallPlanId);
        _logger.LogInformation("Plan generation complete");
        return RedirectToPage(nameof(Hallplan));
    }

    public IActionResult OnPostChangePlayingState()
    {
        InitValues();
        var playerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var player = _db.Players.Single(x => x.Id == playerId);
        var hallPlanEntity = _db.HallPlanEntities
            .Include(hallPlanEntity => hallPlanEntity.Registrations)
            .ThenInclude(hallPlanRegistration => hallPlanRegistration.Player)
            .Single(x => x.Id == SelectedHallPlanEntity!.Id);
        var isRegistered = hallPlanEntity.Registrations.SingleOrDefault(x => x.Player.Id == playerId);

        if (isRegistered == null)
        {
            hallPlanEntity.Registrations.Add(new HallPlanRegistration()
            {
                Player = player,
                RegisteredAt = DateTime.Now
            });
        }
        else
        {
            var hallplanRegistration = hallPlanEntity.Registrations.Single(x => x.Player.Id == playerId);
            hallPlanEntity.Registrations.Remove(hallplanRegistration);
        }

        _db.SaveChanges();

        return RedirectToPage(nameof(Hallplan));
    }

    public IActionResult OnPostAddPlayerToHallplan(int playerId)
    {
        InitValues();
        if (playerId == 0) return RedirectToPage(nameof(Hallplan), new { HallPlanId });

        var exists = _db.HallPlanRegistrations.Any(x => x.PlayerId == playerId && x.HallPlanId == HallPlanId);

        if (exists) return RedirectToPage(nameof(Hallplan));

        var player = _db.Players.Single(x => x.Id == playerId);
        var hallPlanEntity = _db.HallPlanEntities.Single(x => x.Id == HallPlanId);
        _db.HallPlanRegistrations.Add(new HallPlanRegistration
        {
            PlayerId = playerId,
            HallPlanId = HallPlanId,
            Player = player,
            HallPlanEntity = hallPlanEntity
        });

        _db.SaveChanges();


        return RedirectToPage(nameof(Hallplan));
    }

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }

    private void GeneratePlan(DateTime startDate, DateTime endDate, int hallPlanId)
    {
        InitValues();
        // 1️⃣ Alle bestehenden HallPlanDays & Spieler für diesen HallPlan löschen
        var existingDays = _db.HallPlanDays
            .Include(d => d.Players)
            .Where(d => d.HallPlanId == hallPlanId)
            .ToList();

        _db.HallPlanDays.RemoveRange(existingDays);
        _db.SaveChanges();


        var players = _db.HallPlanEntities.Include(hallPlanEntity => hallPlanEntity.Registrations)
            .ThenInclude(hallPlanRegistration => hallPlanRegistration.Player)
            .Single(x => x.Id == SelectedHallPlanEntity!.Id)
            .Registrations
            .Select(x => x.Player)
            .ToList();

        if (players.Count < 4)
        {
            Console.WriteLine("Nicht genug Spieler für den Plan.");
            return;
        }

        // 3️⃣ Generiere Spieltage (jeden Freitag zwischen Start und Ende)
        var matchDays = new List<DateTime>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Friday)
                matchDays.Add(date);
        }

        if (!matchDays.Any())
        {
            Console.WriteLine("Keine Spieltage gefunden.");
            return;
        }

        // 4️⃣ Zähle, wie oft jeder Spieler schon zugewiesen wurde
        var playerCounts = players.ToDictionary(p => p.Id, p => 0);

        var random = new Random();

        foreach (var day in matchDays)
        {
            // Spieler zufällig mischen
            var shuffled = players.OrderBy(p => random.Next()).ToList();

            // 4 Spieler mit den wenigsten Spielen auswählen
            var selectedPlayers = shuffled
                .OrderBy(p => playerCounts[p.Id])
                .Take(4)
                .ToList();

            // HallPlanDay erstellen
            var hallDay = new HallPlanDay
            {
                HallPlanId = hallPlanId,
                PlayDate = day,
                Players = new List<HallPlanDayPlayer>(),
                HallPlanEntity = SelectedHallPlanEntity!
            };

            foreach (var player in selectedPlayers)
            {
                hallDay.Players.Add(new HallPlanDayPlayer
                {
                    Player = player,
                    HallPlanDay = hallDay
                });

                // Spielanzahl erhöhen
                playerCounts[player.Id]++;
            }

            _db.HallPlanDays.Add(hallDay);
        }

        _db.SaveChanges();
        Console.WriteLine("Balanced HallPlan erfolgreich generiert.");
    }

    public IActionResult OnPostSelectHallPlan()
    {
        HttpContext.Session.SetInt32("selectedHallPlanId", HallPlanId);
        return RedirectToPage(nameof(Hallplan));
    }
}