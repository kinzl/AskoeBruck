using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TennisBruck.Dto;
using TennisBruck.Extensions;
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
    public List<TournamentRegistration> RegisteredCompetitionPlayers { get; set; } = [];
    public List<Group> Groups { get; set; } = [];
    public List<Match> PersonalMatches { get; set; }
    public List<Match> AllMatches { get; set; }
    public List<Team> RegisteredTeams { get; set; }
    [BindProperty] public int SelectedSize { get; set; }

    [BindProperty] public List<BracketInput> Inputs { get; set; } = new();

    public List<KnockoutMatch> Matches { get; set; } = new();

    private readonly List<int> _knownBrackets = new() { 2, 4, 8, 16, 32 };
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

    private void InitValues(string? message = null)
    {
        int? selectedCompetitionId = int.Parse(HttpContext.Session.GetString("selectedCompetitionId") ?? "0");
        CurrentPlayer = _currentPlayerService.GetCurrentUser(HttpContext.User.Identities.ToList().First().Name)!;
        Message = message;
        Competitions = _db.Competitions.ToList();

        // DoublePlayers = _db.PlayerCompetitions.Select(x => x.DoublePlayer).ToList();

        PersonalMatches = _db.Matches
            .Include(m => m.Group.Competition)
            .Include(m => m.Team1).ThenInclude(t => t.Players).ThenInclude(tp => tp.Player)
            .Include(m => m.Team2).ThenInclude(t => t.Players).ThenInclude(tp => tp.Player)
            .Include(m => m.Sets)
            .Where(m => m.Team1.Players.Any(tp => tp.PlayerId == CurrentPlayer.Id) ||
                        m.Team2.Players.Any(tp => tp.PlayerId == CurrentPlayer.Id))
            .ToList();


        Matches = _db.KnockoutMatch.ToList();
        if (selectedCompetitionId != 0)
        {
            SelectedCompetition = Competitions.FirstOrDefault(c => c.Id == selectedCompetitionId);

            IsRegistered = _db.TournamentRegistrations
                .Where(x => x.Competition.Id == SelectedCompetition!.Id)
                .Any(x => x.Player.Id == CurrentPlayer.Id);

            RegisteredTeams = _db.Teams
                .Include(x => x.Players)
                .ThenInclude(x => x.Player)
                .Where(x => x.Competition.Id == SelectedCompetition!.Id)
                .ToList();

            RegisteredCompetitionPlayers = _db.TournamentRegistrations
                .Include(x => x.Competition)
                .Include(x => x.Player)
                .Where(x => x.Competition.Id == selectedCompetitionId)
                .ToList();

            Groups = _db.Groups
                .Where(g => g.Competition.Id == selectedCompetitionId)
                .Include(g => g.GroupTeams)
                .ThenInclude(gt => gt.Team)
                .ThenInclude(t => t.Players)
                .ThenInclude(tp => tp.Player)
                .Include(g => g.Competition)
                .ThenInclude(c => c.Teams)
                .ToList();

            AllMatches = _db.Matches
                .Include(x => x.Group)
                .Where(x => x.Group.Competition.Id == SelectedCompetition!.Id)
                .ToList();
            AllMatches.AddRange(_db.KnockoutMatch.Where(x => x.Competition.Id == SelectedCompetition!.Id));

// Sort teams within each group by their points
            foreach (var group in Groups)
            {
                group.GroupTeams = group.GroupTeams
                    .OrderByDescending(gt => gt.Points) // Points should be on GroupTeam
                    .ToList();
            }
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

    public IActionResult OnPostCreateCompetition(string competitionName, bool? isSingle)
    {
        if (competitionName.IsNullOrEmpty() || !isSingle.HasValue)
            return RedirectToPage(new { Message = "Bitte geben Sie einen Namen ein oder Wählen Sie einen Bewerb" });
        _db.Competitions.Add(new Competition
        {
            Name = competitionName,
            IsSingle = isSingle.Value,
            Teams = []
        });
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
        InitValues();
        _db.TournamentRegistrations.Add(new TournamentRegistration()
        {
            Competition = SelectedCompetition!,
            Player = CurrentPlayer,
            RegisteredAt = DateTime.Now
        });
        if (SelectedCompetition!.IsSingle)
        {
            var team = new Team
            {
                CompetitionId = SelectedCompetition.Id,
                Players = new List<TeamPlayer>
                {
                    new() { PlayerId = CurrentPlayer.Id }
                }
            };
            _db.Teams.Add(team);
        }

        _db.SaveChanges();
        return RedirectToPage(new { Message = $"Beim Bewerb angemeldet" });
    }

    public IActionResult OnPostUnregister()
    {
        InitValues();
        var registeredPlayer = _db.TournamentRegistrations
            .SingleOrDefault(x => x.Player.Id == CurrentPlayer.Id && x.Competition.Id == SelectedCompetition!.Id);

        var teamPlayer = _db.TeamPlayer.SingleOrDefault(x =>
            x.Player.Id == CurrentPlayer.Id && x.Team.Competition.Id == SelectedCompetition!.Id);

        var team = _db.Teams.SingleOrDefault(x =>
            x.Competition.Id == SelectedCompetition!.Id && x.Players.Any(y => y.Player.Id == CurrentPlayer.Id));

        if (registeredPlayer != null) _db.TournamentRegistrations.Remove(registeredPlayer);
        if (teamPlayer != null) _db.TeamPlayer.Remove(teamPlayer);
        if (team != null) _db.Teams.Remove(team);


        var groupTeam = _db.GroupTeams.SingleOrDefault(x =>
            x.Group.Competition.Id == SelectedCompetition!.Id &&
            x.Team.Players.Any(y => y.Player.Id == CurrentPlayer.Id));
        if (groupTeam != null)
        {
            _db.GroupTeams.Remove(groupTeam);
        }

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

    public IActionResult OnPostAddSinglePlayer(int teamId, int groupId)
    {
        var group = _db.Groups
            .Include(g => g.Competition)
            .Single(g => g.Id == groupId);

        // Team des Spielers im selben Bewerb suchen
        var existingGroupTeam = _db.GroupTeams
            .Include(gt => gt.Group)
            .ThenInclude(g => g.Competition)
            .Include(gt => gt.Team)
            .ThenInclude(t => t.Players)
            .SingleOrDefault(gt =>
                gt.Team.Id == teamId &&
                gt.Group.Competition.Id == group.Competition.Id
            );

        if (existingGroupTeam != null && existingGroupTeam.GroupId == groupId)
        {
            // optional: TempData["Message"] = "Spieler ist bereits in dieser Gruppe";
            return RedirectToPage();
        }

        if (existingGroupTeam != null)
        {
            existingGroupTeam.GroupId = groupId;
            _db.SaveChanges();
            return RedirectToPage();
        }

        // var team = new Team
        // {
        //     CompetitionId = group.Competition.Id,
        //     Players = new List<TeamPlayer>
        //     {
        //         new TeamPlayer { PlayerId = teamId }
        //     }
        // };
        // _db.Teams.Add(team);
        // _db.SaveChanges();
        var team = _db.Teams.SingleOrDefault(x => x.Id == teamId);
        if (team == null) return RedirectToPage();

        var groupTeam = new GroupTeam
        {
            GroupId = groupId,
            TeamId = team.Id,
            Points = 0
        };
        _db.GroupTeams.Add(groupTeam);
        _db.SaveChanges();

        return RedirectToPage();
    }


    public IActionResult OnPostAddDoubleTeam(int player1Id, int player2Id, int groupId)
    {
        var group = _db.Groups
            .Include(g => g.GroupTeams)
            .ThenInclude(gt => gt.Team)
            .ThenInclude(team => team.Players)
            .Include(group => group.Competition)
            .Single(g => g.Id == groupId);

        // Prüfen, ob Spieler schon in einer Gruppe sind
        bool exists = group.GroupTeams.Any(gt =>
            gt.Team.Players.Any(tp => tp.PlayerId == player1Id || tp.PlayerId == player2Id));
        if (exists)
            return RedirectToPage();

        // Neues Doppel-Team
        var team = new Team
        {
            CompetitionId = group.Competition.Id,
            Players = new List<TeamPlayer>
            {
                new() { PlayerId = player1Id },
                new() { PlayerId = player2Id }
            }
        };
        _db.Teams.Add(team);
        _db.SaveChanges();

        var groupTeam = new GroupTeam
        {
            GroupId = groupId,
            TeamId = team.Id,
            Points = 0
        };
        _db.GroupTeams.Add(groupTeam);
        _db.SaveChanges();

        return RedirectToPage();
    }

    public IActionResult OnPostRemoveTeamFromGroup(int groupId, int teamId)
    {
        var groupTeam = _db.GroupTeams
            .Include(gt => gt.Team)
            .ThenInclude(t => t.Players)
            .Single(gt => gt.GroupId == groupId && gt.TeamId == teamId);

        // Team aus Gruppe entfernen
        _db.GroupTeams.Remove(groupTeam);

        // Optional: Team löschen
        _db.Teams.Remove(groupTeam.Team);

        _db.SaveChanges();
        return RedirectToPage();
    }

    public IActionResult OnPostCreateGroup()
    {
        InitValues();
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

    public IActionResult OnPostSaveGroups()
    {
        InitValues();

        // Alte Matches löschen
        var removedMatches = _db.Matches
            .Where(m => m.Group.Competition.Id == SelectedCompetition!.Id)
            .ToList();

        _db.Matches.RemoveRange(removedMatches);
        _db.SaveChanges();

        // Gruppen mit Teams laden
        var groups = _db.Groups
            .Where(g => g.Competition.Id == SelectedCompetition!.Id)
            .Include(g => g.GroupTeams)
            .ThenInclude(gt => gt.Team)
            .ThenInclude(t => t.Players) // TeamPlayer
            .ThenInclude(tp => tp.Player)
            .ToList();

        // Matches erzeugen (Round-Robin)
        foreach (var group in groups)
        {
            var teams = group.GroupTeams
                .Select(gt => gt.Team)
                .ToList();

            for (int i = 0; i < teams.Count; i++)
            {
                for (int j = i + 1; j < teams.Count; j++)
                {
                    _db.Matches.Add(new Match
                    {
                        Group = group,
                        Team1 = teams[i],
                        Team2 = teams[j]
                    });
                }
            }
        }

        _db.SaveChanges();

        return RedirectToPage(new { Message = "Spiele wurden erstellt" });
    }


    public IActionResult OnPostSaveMatch(string score, int matchId)
    {
        try
        {
            int setsWonPlayer1 = 0;
            int setsWonPlayer2 = 0;
            var match = _db.Matches
                .Include(x => x.Sets)
                .Include(x => x.Team1)
                .Include(x => x.Team2)
                .Include(x => x.Group)
                .Single(x => x.Id == matchId);
            var sets = score.Split(" ");
            for (var i = 0; i < sets.Length; i++)
            {
                var games = sets[i].Split(":");
                if (int.Parse(games[0]) < int.Parse(games[1]))
                    setsWonPlayer2++;
                else
                    setsWonPlayer1++;
                match.Sets?.Add(new Set
                {
                    SetNumber = i + 1,
                    Player1GamesWon = int.Parse(games[0]),
                    Player2GamesWon = int.Parse(games[1]),
                });
            }

            if (setsWonPlayer1 == setsWonPlayer2)
                return RedirectToPage(new { Message = "Unentschieden ist nicht erlaubt" });
            var winner = setsWonPlayer1 > setsWonPlayer2 ? match.Team1 : match.Team2;
            if (match is not KnockoutMatch)
            {
                var groupPlayer = _db.GroupTeams
                    .Single(x => x.Group.Id == match.Group!.Id && x.Team.Id == winner.Id);
                groupPlayer.Points += 3;
            }

            match.Winner = winner;

            _db.SaveChanges();
        }
        catch (Exception)
        {
            return RedirectToPage(new
                { Message = "Fehler beim Speichern des Spiels (Falsche eingabe des Spielstandes?)" });
        }

        return RedirectToPage(new
            { Message = "Spiele wurden gespeichert" });
    }

    public IActionResult OnPostDeleteMatch(int matchId)
    {
        var match = _db.Matches
            .Include(x => x.Sets)
            .Include(x => x.Group)
            .Include(x => x.Winner)
            .Single(x => x.Id == matchId);
        match.Sets?.Clear();
        if (match is not KnockoutMatch)
        {
            var groupTeam = _db.GroupTeams
                .Single(x => x.Group.Id == match.Group!.Id && x.Team.Id == match.Winner!.Id);
            groupTeam.Points -= 3;
        }

        match.Winner = null;
        _db.SaveChanges();

        return RedirectToPage();
    }

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }

    public IActionResult OnPostCreateBracket()
    {
        InitValues();
        if (!_knownBrackets.Contains(SelectedSize)) return RedirectToPage();

        UpdateBracket(SelectedSize);
        // OnPostApplyUserInputs();
        return RedirectToPage();
    }

    private void UpdateBracket(int size)
    {
        _db.KnockoutMatch.ExecuteDelete();
        _db.SaveChanges();
        int closest = _knownBrackets.First(k => k >= size);
        int byes = closest - size;
        if (byes > 0) size = closest;

        int round = 1;
        double baseT = size / 2;
        double baseC = size / 2;
        int matchId = 1;
        int nextInc = size / 2;

        for (int i = 1; i <= (size - 1); i++)
        {
            double baseR = i / baseT;
            bool isBye = byes > 0 && (i % 2 != 0 || byes >= (baseT - i));

            if (isBye) byes--;

            _db.KnockoutMatch.Add(new KnockoutMatch()
            {
                CompetitionId = SelectedCompetition!.Id,
                BracketNo = matchId++,
                RoundNo = round,
                IsBye = isBye,
                NextGame = nextInc + i > size - 1 ? null : nextInc + i
            });

            if (i % 2 != 0) nextInc--;

            while (baseR >= 1)
            {
                round++;
                baseC /= 2;
                baseT += baseC;
                baseR = i / baseT;
            }
        }

        _db.SaveChanges();
    }

    public IActionResult OnPostApplyUserInputs()
    {
        InitValues();

        foreach (var match in Matches)
        {
            var input = Inputs.FirstOrDefault(i => i.BracketNo == match.BracketNo);

            if (input != null)
            {
                match.Team1 = _db.Teams
                    .Include(t => t.Players)
                    .ThenInclude(tp => tp.Player)
                    .SingleOrDefault(t => t.Id == input.Team1Id);

                match.Team2 = _db.Teams
                    .Include(t => t.Players)
                    .ThenInclude(tp => tp.Player)
                    .SingleOrDefault(t => t.Id == input.Team2Id);
            }
        }

        _db.SaveChanges();
        return RedirectToPage();
    }

    public IActionResult OnPostSavePairs(List<PlayerCompetitionPairs> pairs)
    {
        InitValues();

        foreach (var pair in pairs)
        {
            if (!pair.SinglePlayerId.HasValue || !pair.DoublePlayerId.HasValue)
                continue;

            int player1Id = pair.SinglePlayerId.Value;
            int player2Id = pair.DoublePlayerId.Value;

            // Prüfen, ob Spieler schon in einem Team in dieser Competition sind
            bool exists = _db.Teams
                .Include(t => t.Players)
                .Any(t => t.Players.Any(tp => tp.PlayerId == player1Id || tp.PlayerId == player2Id)
                          && t.CompetitionId == SelectedCompetition!.Id);

            if (exists)
                continue; // Spieler bereits vergeben

            // Neues Doppel-Team erstellen
            var team = new Team
            {
                CompetitionId = SelectedCompetition!.Id,
                Players = new List<TeamPlayer>
                {
                    new() { PlayerId = player1Id },
                    new() { PlayerId = player2Id }
                }
            };

            _db.Teams.Add(team);
        }

        _db.SaveChanges(); // Save all teams at once
        return RedirectToPage();
    }
}