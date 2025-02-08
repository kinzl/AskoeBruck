using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;
using TennisBruck.Services;
using TennisDb;

namespace TennisBruck.Pages;

[Authorize]
public class Championship : PageModel
{
    private TennisContext _db;
    private CurrentPlayerService _currentPlayerService;
    public Player CurrentPlayer { get; set; }
    [BindProperty] public List<Competition> Competitions { get; set; }
    [BindProperty] public Competition? SelectedCompetition { get; set; }
    [BindProperty] public int? SelectedCompetitionId { get; set; }
    [BindProperty] public bool IsRegistered { get; set; }
    [BindProperty] public List<Competition> RegisteredCompetitions { get; set; }
    [BindProperty] public List<Player> RegisteredCompetitionPlayers { get; set; } = new();
    public string? Message { get; set; }

    public Championship(CurrentPlayerService currentPlayerService, TennisContext db)
    {
        _currentPlayerService = currentPlayerService;
        _db = db;
    }


    public void OnGet(int? selectedCompetitionId, string? message)
    {
        InitValues(selectedCompetitionId, message);
    }

    public void OnPost(int? selectedCompetitionId, string? message)
    {
        InitValues(selectedCompetitionId, message);
    }

    private void InitValues(int? selectedCompetitionId, string? message)
    {
        if (selectedCompetitionId.HasValue) SelectedCompetitionId = selectedCompetitionId;
        Message = message;
        CurrentPlayer = _currentPlayerService.GetCurrentUser(HttpContext.User.Identities.ToList().First().Name)!;
        Competitions = _db.Competitions.ToList();

        RegisteredCompetitions = _db.PlayerCompetitions.Where(x => x.Player.Id == CurrentPlayer.Id)
            .Select(x => x.Competition).ToList();

        if (SelectedCompetitionId.HasValue)
        {
            SelectedCompetition = Competitions.FirstOrDefault(c => c.Id == SelectedCompetitionId);
            IsRegistered = _db.PlayerCompetitions.SingleOrDefault(x =>
                x.Player.Id == CurrentPlayer.Id && x.Competition.Id == SelectedCompetitionId) != null;
            RegisteredCompetitionPlayers = _db.PlayerCompetitions
                .Where(x => x.Competition.Id == SelectedCompetitionId)
                .Select(x => x.Player).ToList();
        }
    }

    public IActionResult OnPostDeleteCompetition(int competitionId)
    {
        var competition = _db.Competitions.Find(competitionId);
        if (competition == null)
            return RedirectToPage(new
                { selectedCompetitionId = competitionId, Message = "Ein Fehler ist aufgetreten" });

        _db.Competitions.Remove(competition);
        _db.SaveChanges();
        return RedirectToPage(new { Message = "Bewerb wurde gelöscht" });
    }

    public IActionResult OnPostCreateCompetition(string competitionName)
    {
        if (competitionName.IsNullOrEmpty()) return BadRequest();
        _db.Competitions.Add(new Competition { Name = competitionName });
        _db.SaveChanges();
        return RedirectToPage(new { Message = "Neuer Bewerb erstellt" });
    }

    public IActionResult OnPostCompetitionChanged(int selectedCompetition)
    {
        InitValues(selectedCompetition, null);
        SelectedCompetition = Competitions[selectedCompetition - 1];
        return Page();
    }

    public IActionResult OnPostRegister(int selectedCompetition)
    {
        InitValues(selectedCompetition, null);
        _db.PlayerCompetitions.Add(new PlayerCompetition
        {
            Player = CurrentPlayer,
            Competition = _db.Competitions.Find(selectedCompetition)!
        });
        _db.SaveChanges();
        return RedirectToPage(new { selectedCompetitionId = selectedCompetition, Message = "Beim Bewerb abgemeldet" });
    }

    public IActionResult OnPostUnregister(int selectedCompetition)
    {
        InitValues(selectedCompetition, null);
        var playerCompetition = _db.PlayerCompetitions.SingleOrDefault(x =>
            x.Player.Id == CurrentPlayer.Id && x.Competition.Id == selectedCompetition);
        if (playerCompetition == null) return NotFound();
        _db.PlayerCompetitions.Remove(playerCompetition);
        _db.SaveChanges();
        return RedirectToPage(new { selectedCompetitionId = selectedCompetition, Message = "Vom Bewerb abgemeldet" });
    }

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }
}