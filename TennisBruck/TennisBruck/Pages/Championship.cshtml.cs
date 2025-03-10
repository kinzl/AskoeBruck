using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TennisBruck.Services;
using TennisDb;

namespace TennisBruck.Pages;

[Authorize]
[BindProperties]
public class Championship : PageModel
{
    private TennisContext _db;
    private CurrentPlayerService _currentPlayerService;
    public Player CurrentPlayer { get; set; }

    public List<Competition> Competitions { get; set; }
    public Competition? SelectedCompetition { get; set; }
    public bool IsRegistered { get; set; }
    public List<Competition> RegisteredCompetitions { get; set; }
    public List<Player> RegisteredCompetitionPlayers { get; set; } = new();
    public List<Group> Groups { get; set; } = new();
    public string? Message { get; set; }

    public Championship(CurrentPlayerService currentPlayerService, TennisContext db)
    {
        _currentPlayerService = currentPlayerService;
        _db = db;
    }


    public void OnGet(string? message)
    {
        InitValues(message);
    }

    public void OnPost(string? message)
    {
        InitValues(message);
    }

    private void InitValues(string? message)
    {
        int? selectedCompetitionId = int.Parse(HttpContext.Session.GetString("selectedCompetitionId") ?? "0");
        CurrentPlayer = _currentPlayerService.GetCurrentUser(HttpContext.User.Identities.ToList().First().Name)!;
        Message = message;
        Competitions = _db.Competitions.ToList();

        RegisteredCompetitions = _db.PlayerCompetitions.Where(x => x.Player.Id == CurrentPlayer.Id)
            .Select(x => x.Competition).ToList();

        if (selectedCompetitionId != 0)
        {
            SelectedCompetition = Competitions.FirstOrDefault(c => c.Id == selectedCompetitionId);
            IsRegistered = _db.PlayerCompetitions.SingleOrDefault(x =>
                x.Player.Id == CurrentPlayer.Id && x.Competition.Id == selectedCompetitionId) != null;
            RegisteredCompetitionPlayers = _db.PlayerCompetitions
                .Include(x => x.Player.GroupPlayers)
                .Where(x => x.Competition.Id == selectedCompetitionId)
                .Select(x => x.Player).ToList();

            Groups = _db.Groups
                .Where(x => x.Competition.Id == selectedCompetitionId)
                .Include(x => x.GroupPlayers)
                .ToList();
        }
    }

    public IActionResult OnPostDeleteCompetition(int competitionId)
    {
        var competition = _db.Competitions.Find(competitionId);
        if (competition == null) return RedirectToPage(new { Message = "Ein Fehler ist aufgetreten" });

        _db.Competitions.Remove(competition);
        _db.SaveChanges();
        return RedirectToPage(new { Message = "Bewerb wurde gelöscht" });
    }

    public IActionResult OnPostCreateCompetition(string competitionName)
    {
        if (competitionName.IsNullOrEmpty()) return RedirectToPage(new { Message = "Bitte geben Sie einen Namen ein" });
        _db.Competitions.Add(new Competition { Name = competitionName });
        _db.SaveChanges();
        return RedirectToPage(new { Message = "Neuer Bewerb erstellt" });
    }

    public IActionResult OnPostCompetitionChanged(int selectedCompetitionId)
    {
        HttpContext.Session.SetString("selectedCompetitionId", selectedCompetitionId.ToString());
        return RedirectToPage();
    }

    public IActionResult OnPostRegister()
    {
        InitValues(null);
        _db.PlayerCompetitions.Add(new PlayerCompetition
        {
            Player = CurrentPlayer,
            Competition = SelectedCompetition!
        });
        _db.SaveChanges();
        return RedirectToPage(new { Message = "Beim Bewerb abgemeldet" });
    }

    public IActionResult OnPostUnregister()
    {
        InitValues(null);
        var playerCompetition = _db.PlayerCompetitions.SingleOrDefault(x =>
            x.Player.Id == CurrentPlayer.Id && x.Competition.Id == SelectedCompetition!.Id);
        if (playerCompetition == null) return NotFound();
        _db.PlayerCompetitions.Remove(playerCompetition);
        _db.SaveChanges();
        return RedirectToPage(new { Message = "Vom Bewerb abgemeldet" });
    }

    public IActionResult OnPostIncreaseGroupSize(int groupId)
    {
        var group = _db.Groups.Single(x => x.Id == groupId);
        group.MaxAmount++;
        _db.SaveChanges();
        return RedirectToPage();
    }

    public IActionResult OnPostDecreaseGroupSize(int groupId)
    {
        var group = _db.Groups.Single(x => x.Id == groupId);
        if (group.MaxAmount == 1) return RedirectToPage();
        group.MaxAmount--;
        _db.SaveChanges();
        return RedirectToPage();
    }

    public IActionResult OnPostAddPlayerToGroup(int playerId, int groupId)
    {
        if (_db.GroupPlayers.Any(x => x.PlayerId == playerId))
        {
            var groupPlayer = _db.GroupPlayers.Single(x => x.PlayerId == playerId);
            groupPlayer.GroupId = groupId;
        }
        else
        {
            _db.GroupPlayers.Add(new GroupPlayer()
            {
                GroupId = groupId,
                PlayerId = playerId
            });
        }

        _db.SaveChanges();
        return RedirectToPage();
    }

    public IActionResult OnPostRemovePlayerFromGroup(int groupId, int playerId)
    {
        var groupPlayer = _db.GroupPlayers
            .Include(x => x.Player)
            .Include(x => x.Group)
            .Single(x => x.PlayerId == playerId && x.GroupId == groupId);
        _db.GroupPlayers.Remove(groupPlayer);
 
        _db.SaveChanges();
        return RedirectToPage();
    }

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }
}