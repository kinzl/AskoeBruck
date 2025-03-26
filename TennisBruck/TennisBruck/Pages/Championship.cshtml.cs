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
    public bool IsRegistered { get; set; }
    private CurrentPlayerService _currentPlayerService;
    public Player CurrentPlayer { get; set; }

    public List<Competition> Competitions { get; set; }
    public Competition? SelectedCompetition { get; set; }
    public List<Competition> RegisteredCompetitions { get; set; }
    public List<Player> RegisteredCompetitionPlayers { get; set; } = new();
    public List<Group> Groups { get; set; } = new();
    public List<Match> PersonalMatches { get; set; }
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

        PersonalMatches = _db.Matches
            .Include(x => x.Group.Competition)
            .Where(x => x.Player1 == CurrentPlayer || x.Player2 == CurrentPlayer)
            .ToList();

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
        return RedirectToPage(new { Message = $"Beim Bewerb anemeldet" });
    }

    public IActionResult OnPostUnregister()
    {
        InitValues(null);
        var playerCompetition = _db.PlayerCompetitions.Single(x =>
            x.Player.Id == CurrentPlayer.Id && x.Competition.Id == SelectedCompetition!.Id);
        _db.PlayerCompetitions.Remove(playerCompetition);

        var groupPlayers = _db.GroupPlayers.SingleOrDefault(x =>
            x.PlayerId == CurrentPlayer.Id && x.PlayerId == playerCompetition.Player.Id);
        if (groupPlayers != null) _db.GroupPlayers.Remove(groupPlayers);

        _db.SaveChanges();
        return RedirectToPage(new { Message = $"Vom Bewerb abgemeldet" });
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

    public IActionResult OnPostCreateGroup()
    {
        InitValues(null);
        _db.Groups.Add(new Group
        {
            Competition = SelectedCompetition!,
            MaxAmount = 1,
            GroupName = "Gruppe "
        });
        _db.SaveChanges();

        var groups = _db.Groups.Where(x => x.Competition.Id == SelectedCompetition!.Id).ToList();

        for (int i = 0; i < groups.Count; i++)
        {
            groups[i].GroupName = $"Gruppe {(char)(i + 65)}";
        }

        _db.SaveChanges();

        return RedirectToPage();
    }

    public IActionResult OnPostDeleteGroup(int groupId)
    {
        var selectedGroup = _db.Groups.Single(x => x.Id == groupId);
        _db.Groups.Remove(selectedGroup);
        _db.SaveChanges();
        return RedirectToPage();
    }

    public IActionResult OnPostRemovePlayerFromCompetition(int playerId)
    {
        var playerCompetition = _db.PlayerCompetitions.Single(x =>
            x.Id == playerId && x.Competition.Id == SelectedCompetition!.Id);
        _db.PlayerCompetitions.Remove(playerCompetition);
        _db.SaveChanges();
        return RedirectToPage();
    }

    public IActionResult OnPostSaveGroups()
    {
        //Create matches for the groups
        InitValues(null);
        var competitionGroup = _db.Groups.Where(x => x.Competition.Id == SelectedCompetition!.Id)
            .Include(group => group.GroupPlayers).ThenInclude(groupPlayer => groupPlayer.Player).ToList();
        foreach (var group in competitionGroup)
        {
            var groupPlayers = group.GroupPlayers;
            for (int i = 0; i < groupPlayers.Count; i++)
            {
                for (int j = i + 1; j < groupPlayers.Count; j++)
                {
                    _db.Matches.Add(new Match
                    {
                        Player1 = groupPlayers[i].Player,
                        Player2 = groupPlayers[j].Player,
                        Group = group,
                        Sets = new List<Set>()
                    });
                }
            }
        }

        _db.SaveChanges();
        return RedirectToPage(new { Message = "Spiele wurden erstellt" });
    }

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }
}