using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
    public List<Competition> RegisteredCompetitions { get; set; }
    public List<Player> RegisteredCompetitionPlayers { get; set; } = new();
    public List<Group> Groups { get; set; } = new();
    public List<Match> PersonalMatches { get; set; }
    public List<Player?> DoublePlayers { get; set; }
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

        DoublePlayers = _db.PlayerCompetitions.Select(x => x.DoublePlayer).ToList();

        PersonalMatches = _db.Matches
            .Include(x => x.Group.Competition)
            .Include(x => x.Player1)
            .Include(x => x.Player2)
            .Include(x => x.Sets)
            .Where(x => x.Player1 == CurrentPlayer || x.Player2 == CurrentPlayer ||
                        (x.DoublePlayer1 != null && x.DoublePlayer1 == CurrentPlayer) ||
                        (x.DoublePlayer2 != null && x.DoublePlayer2 == CurrentPlayer))
            .ToList();

        RegisteredCompetitions = _db.PlayerCompetitions
            .Where(x => x.SinglePlayer.Id == CurrentPlayer.Id)
            .Select(x => x.Competition)
            .ToList();
        Matches = _db.KnockoutMatch.ToList();
        if (selectedCompetitionId != 0)
        {
            SelectedCompetition = Competitions.FirstOrDefault(c => c.Id == selectedCompetitionId);

            IsRegistered = _db.PlayerCompetitions.SingleOrDefault(x =>
                (x.SinglePlayer.Id == CurrentPlayer.Id && x.Competition.Id == selectedCompetitionId) ||
                (x.DoublePlayer.Id == CurrentPlayer.Id && x.Competition.Id == selectedCompetitionId)) != null;


            RegisteredCompetitionPlayers = _db.PlayerCompetitions
                .Include(x => x.SinglePlayer.GroupPlayers)
                .Where(x => x.Competition.Id == selectedCompetitionId)
                .Select(x => x.SinglePlayer).ToList();

            Groups = _db.Groups
                .Include(x => x.GroupPlayers)
                .ThenInclude(x => x.Player)
                .Include(x => x.Competition)
                .ThenInclude(x => x.PlayerCompetitions)
                .ThenInclude(x => x.SinglePlayer)
                .Where(x => x.Competition.Id == selectedCompetitionId)
                .ToList();

// Sort players within each group by their points
            foreach (var group in Groups)
            {
                group.GroupPlayers = group.GroupPlayers
                    .OrderByDescending(p => p.Points)
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
            PlayerCompetitions = new List<PlayerCompetition>()
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
        _db.PlayerCompetitions.Add(new PlayerCompetition
        {
            SinglePlayer = CurrentPlayer,
            Competition = SelectedCompetition!
        });
        _db.SaveChanges();
        return RedirectToPage(new { Message = $"Beim Bewerb angemeldet" });
    }

    public IActionResult OnPostUnregister()
    {
        InitValues();
        if (SelectedCompetition!.IsSingle)
        {
            var playerCompetition = _db.PlayerCompetitions.Single(x =>
                x.SinglePlayer.Id == CurrentPlayer.Id && x.Competition.Id == SelectedCompetition!.Id);
            _db.PlayerCompetitions.Remove(playerCompetition);

            var groupPlayers = _db.GroupPlayers.SingleOrDefault(x =>
                x.PlayerId == CurrentPlayer.Id && x.PlayerId == playerCompetition.SinglePlayer.Id);
            if (groupPlayers != null) _db.GroupPlayers.Remove(groupPlayers);
        }
        else
        {
            var playerCompetition = _db.PlayerCompetitions
                .Include(x => x.SinglePlayer)
                .Include(x => x.DoublePlayer)
                .SingleOrDefault(x =>
                    x.SinglePlayer.Id == CurrentPlayer.Id && x.Competition.Id == SelectedCompetition!.Id);

            if (playerCompetition != null)
            {
                if (playerCompetition.DoublePlayer != null)
                {
                    playerCompetition.SinglePlayer = playerCompetition.DoublePlayer;
                    playerCompetition.DoublePlayer = null;
                }
                else
                {
                    _db.PlayerCompetitions.Remove(playerCompetition);
                }
            }
            else
            {
                playerCompetition = _db.PlayerCompetitions
                    .Include(x => x.SinglePlayer)
                    .Include(x => x.DoublePlayer)
                    .SingleOrDefault(x =>
                        x.DoublePlayer!.Id == CurrentPlayer.Id && x.Competition.Id == SelectedCompetition!.Id);
                playerCompetition!.DoublePlayer = null;
            }

            var groupPlayers = _db.GroupPlayers.SingleOrDefault(x =>
                x.PlayerId == CurrentPlayer.Id && x.Group.Competition.Id == SelectedCompetition!.Id);

            if (groupPlayers != null)
            {
                _db.GroupPlayers.Remove(groupPlayers);
            }
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

    public IActionResult OnPostAddSinglePlayer(int playerId, int groupId, int competitionId)
    {
        if (_db.GroupPlayers.Any(x => x.PlayerId == playerId && x.Group.Competition.Id == competitionId))
        {
            var groupPlayer = _db.GroupPlayers
                .Include(x => x.Player)
                .Include(x => x.Group)
                .Single(x => x.PlayerId == playerId && x.Group.Competition.Id == competitionId);
            groupPlayer.GroupId = groupId;
        }
        else
        {
            _db.GroupPlayers.Add(new GroupPlayer()
            {
                GroupId = groupId,
                PlayerId = playerId,
            });
        }

        _db.SaveChanges();
        return RedirectToPage();
    }

    public IActionResult OnPostAddDoublePlayer(int doublePlayerId, int playerId, int competitionId)
    {
        var playerCompetition = _db.PlayerCompetitions
            .Include(x => x.SinglePlayer)
            .Include(x => x.Competition)
            .SingleOrDefault(x => x.SinglePlayer.Id == playerId && x.Competition.Id == competitionId);
        if (playerCompetition == null) return RedirectToPage();

        var doublePlayer = _db.PlayerCompetitions
            .Include(x => x.SinglePlayer)
            .Include(x => x.Competition)
            .ThenInclude(x => x.PlayerCompetitions)
            .Single(x => x.SinglePlayer.Id == doublePlayerId && x.Competition.Id == competitionId);

        playerCompetition.DoublePlayer = doublePlayer.SinglePlayer;
        _db.PlayerCompetitions.Remove(doublePlayer);

        _db.SaveChanges();

        return RedirectToPage();
    }

    public IActionResult OnPostRemovePlayerFromGroup(int groupId, int playerId, int? doublePlayerId)
    {
        var groupPlayer = _db.GroupPlayers
            .Include(x => x.Player)
            .Include(x => x.Group).ThenInclude(group => group.Competition)
            .ThenInclude(competition => competition.PlayerCompetitions)
            .ThenInclude(playerCompetition => playerCompetition.DoublePlayer)
            .Single(x => x.PlayerId == playerId && x.GroupId == groupId);


        if (doublePlayerId.HasValue)
        {
            var doublePlayer = groupPlayer.Group.Competition.PlayerCompetitions
                .Single(x => x.DoublePlayer != null && x.DoublePlayer.Id == doublePlayerId.Value).DoublePlayer;
            if (doublePlayer != null)
                _db.PlayerCompetitions.Add(new PlayerCompetition
                {
                    SinglePlayer = doublePlayer,
                    Competition = groupPlayer.Group.Competition,
                    DoublePlayer = null
                });
            groupPlayer.Group.Competition.PlayerCompetitions
                .Single(x => x.DoublePlayer != null && x.DoublePlayer.Id == doublePlayerId).DoublePlayer = null;
        }

        _db.GroupPlayers.Remove(groupPlayer);
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

    public IActionResult OnPostRemovePlayerFromCompetition(int playerId)
    {
        //ToDo: Remove player from all groups and matches
        var playerCompetition = _db.PlayerCompetitions.Single(x =>
            x.Id == playerId && x.Competition.Id == SelectedCompetition!.Id);
        _db.PlayerCompetitions.Remove(playerCompetition);
        _db.SaveChanges();
        return RedirectToPage();
    }

    public IActionResult OnPostSaveGroups()
    {
        //Create matches for the groups
        InitValues();
        var removedMatches = _db.Matches.Where(x => x.Group.Competition.Id == SelectedCompetition!.Id).ToList();
        _db.RemoveRange(removedMatches);
        _db.SaveChanges();

        var competitionGroup = _db.Groups.Where(x => x.Competition.Id == SelectedCompetition!.Id)
            .Include(group => group.GroupPlayers).ThenInclude(groupPlayer => groupPlayer.Player)
            .ThenInclude(player => player.GroupPlayers)
            .Include(group => group.Competition).ThenInclude(competition => competition.PlayerCompetitions)
            .ThenInclude(playerCompetition => playerCompetition.DoublePlayer).Include(group => group.Competition)
            .ThenInclude(competition => competition.PlayerCompetitions)
            .ThenInclude(playerCompetition => playerCompetition.SinglePlayer)
            .ToList();

        foreach (var group in competitionGroup)
        {
            var groupPlayers = group.GroupPlayers;
            for (int i = 0; i < groupPlayers.Count; i++)
            {
                for (int j = i + 1; j < groupPlayers.Count; j++)
                {
                    var team1 = groupPlayers[i].Group.Competition.PlayerCompetitions
                        .Single(x => x.SinglePlayer.Id == groupPlayers[i].Player.Id);
                    var team2 = groupPlayers[j].Group.Competition.PlayerCompetitions
                        .Single(x => x.SinglePlayer.Id == groupPlayers[j].Player.Id);
                    _db.Matches.Add(new Match
                    {
                        Player1 = team1.SinglePlayer,
                        DoublePlayer1 = team1.DoublePlayer,
                        Player2 = team2.SinglePlayer,
                        DoublePlayer2 = team2.DoublePlayer,
                        Group = group,
                        Sets = []
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
                .Include(x => x.Player1)
                .Include(x => x.Player2)
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
            var winner = setsWonPlayer1 > setsWonPlayer2 ? match.Player1 : match.Player2;
            if (match is not KnockoutMatch)
            {
                var groupPlayer = _db.GroupPlayers
                    .Single(x => x.Group.Id == match.Group!.Id && x.Player.Id == winner.Id);
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
            var groupPlayer = _db.GroupPlayers
                .Single(x => x.Group.Id == match.Group!.Id && x.Player.Id == match.Winner!.Id);
            groupPlayer.Points -= 3;
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
                match.Player1 = RegisteredCompetitionPlayers.FirstOrDefault(p => p.Id == input.Player1Id);
                match.Player2 = RegisteredCompetitionPlayers.FirstOrDefault(p => p.Id == input.Player2Id);
                _db.SaveChanges();
            }
        }

        return RedirectToPage();
    }
}