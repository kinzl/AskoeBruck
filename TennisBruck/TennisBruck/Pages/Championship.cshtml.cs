using Microsoft.AspNetCore.Identity;
using Group = TennisDb.Group;
using Match = TennisDb.Match;

namespace TennisBruck.Pages;

[Authorize]
[BindProperties]
public class Championship : PageModel
{
    private TennisContext _db;
    public bool IsRegistered { get; set; }
    private CurrentPlayerService _currentPlayerService;
    public required Player CurrentPlayer { get; set; }

    public required List<Competition> Competitions { get; set; }
    public Competition? SelectedCompetition { get; set; }
    public List<TournamentRegistration> RegisteredCompetitionPlayers { get; set; } = [];
    public List<Group> Groups { get; set; } = [];
    public required List<Match> PersonalMatches { get; set; }
    public required List<Match> AllMatches { get; set; }
    public required List<Team> RegisteredTeams { get; set; }
    [BindProperty] public int SelectedSize { get; set; }

    [BindProperty] public List<BracketInput> Inputs { get; set; } = new();

    public List<KnockoutMatch> Matches { get; set; } = new();

    private readonly List<int> _knownBrackets = new() { 2, 4, 8, 16, 32 };
    private UserManager<IdentityUser> _userManager;
    public string? Message { get; set; }
    public required List<Player> UnregisteredPlayers { get; set; }
    public Dictionary<int, List<GroupTableEntry>> GroupTables { get; set; } = new();

    public Championship(CurrentPlayerService currentPlayerService, TennisContext db,
        UserManager<IdentityUser> userManager)
    {
        _currentPlayerService = currentPlayerService;
        _db = db;
        _userManager = userManager;
    }

    public void OnGet(string? message)
    {
        InitValues(message);
    }

    private void InitValues(string? message = null)
    {
        int? selectedCompetitionId = int.Parse(HttpContext.Session.GetString("selectedCompetitionId") ?? "0");
        CurrentPlayer = _currentPlayerService.GetCurrentUser()!;
        Message = message;
        Competitions = _db.Competitions.ToList();

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

            UnregisteredPlayers = _db.Players
                .Where(p => p.TournamentRegistrations.All(r => r.CompetitionId != SelectedCompetition!.Id))
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
                .ThenInclude(x => x.Competition)
                .Include(x => x.Team1)
                .Include(x => x.Team2)
                .Include(x => x.Sets)
                .Where(x => x.Group != null && x.Group.Competition.Id == SelectedCompetition!.Id)
                .ToList();

            AllMatches.AddRange(_db.KnockoutMatch
                .Include(x => x.Group)
                .ThenInclude(x => x.Competition)
                .Include(x => x.Team1)
                .Include(x => x.Team2)
                .Include(x => x.Sets)
                .Where(x => x.Competition.Id == SelectedCompetition!.Id));

            foreach (var group in Groups)
            {
                // Holt alle Matches für genau diese Gruppe
                var matchesForGroup = AllMatches.Where(m => m.Group?.Id == group.Id).ToList();

                // Berechnet die Tabelle und speichert sie im Dictionary unter der Gruppen-ID
                GroupTables[group.Id] = CalculateGroupTable(group.GroupTeams, matchesForGroup);
            }
        }
    }

    #region CRUD Competition

    public IActionResult OnPostDeleteCompetition(int competitionId)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        var competition = _db.Competitions.Find(competitionId);
        if (competition == null) return RedirectToPage(new { Message = "Ein Fehler ist aufgetreten" });

        _db.Competitions.Remove(competition);
        _db.SaveChanges();
        HttpContext.Session.SetString("selectedCompetitionId", "0");
        return RedirectToPage(new { Message = "Bewerb wurde gelöscht" });
    }

    public IActionResult OnPostCreateCompetition(string competitionName, bool? isSingle)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        if (competitionName.IsNullOrEmpty() || !isSingle.HasValue)
            return RedirectToPage(new { Message = "Bitte geben Sie einen Namen ein oder Wählen Sie einen Bewerb" });
        _db.Competitions.Add(new Competition
        {
            Name = competitionName,
            IsSingle = isSingle.Value,
            RegistrationUntil = DateTime.Now.AddDays(14),
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

    #endregion

    #region registration

    public IActionResult OnPostRegister(int? playerId = null)
    {
        InitValues();
        _db.TournamentRegistrations.Add(new TournamentRegistration()
        {
            Competition = SelectedCompetition!,
            PlayerId = playerId ?? CurrentPlayer.Id,
            RegisteredAt = DateTime.Now
        });
        if (SelectedCompetition!.IsSingle)
        {
            var team = new Team
            {
                CompetitionId = SelectedCompetition.Id,
                Players = new List<TeamPlayer>
                {
                    new() { PlayerId = playerId ?? CurrentPlayer.Id }
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

    #endregion

    #region Group Management

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

        // Delete old matches
        var removedMatches = _db.Matches
            .Where(m => m.Group.Competition.Id == SelectedCompetition!.Id)
            .ToList();

        _db.Matches.RemoveRange(removedMatches);
        _db.SaveChanges();

        // load groups with teams
        var groups = _db.Groups
            .Where(g => g.Competition.Id == SelectedCompetition!.Id)
            .Include(g => g.GroupTeams)
            .ThenInclude(gt => gt.Team)
            .ThenInclude(t => t.Players) // TeamPlayer
            .ThenInclude(tp => tp.Player)
            .ToList();

        // create matches (Robin round)
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

    public IActionResult OnPostIncreaseGroupSize(int groupId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        var group = _db.Groups.Single(x => x.Id == groupId);
        group.MaxAmount++;
        _db.SaveChanges();
        return RedirectToPage();
    }

    public IActionResult OnPostDecreaseGroupSize(int groupId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        var group = _db.Groups.Single(x => x.Id == groupId);
        if (group.MaxAmount == 1) return RedirectToPage();
        group.MaxAmount--;
        _db.SaveChanges();
        return RedirectToPage();
    }

    #endregion

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

        var team = _db.Teams.SingleOrDefault(x => x.Id == teamId);
        if (team == null) return RedirectToPage();

        var groupTeam = new GroupTeam
        {
            GroupId = groupId,
            TeamId = team.Id
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

        _db.GroupTeams.Remove(groupTeam);

        _db.SaveChanges();
        return RedirectToPage();
    }

    #region Match Management

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

    public async Task<IActionResult> OnPostDeleteMatchAsync(int matchId)
    {
        // WICHTIG: .Include(m => m.Sets) hinzufügen, damit er die Sätze auch findet!
        var match = await _db.Matches
            .Include(m => m.Sets)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null) return NotFound();

        bool isAdmin = User.IsInRole("Admin");
        if (match.IsWalkover && !isAdmin)
        {
            Message = "Ein Walkover kann nur vom Admin rückgängig gemacht werden.";
            return RedirectToPage();
        }

        // --- NEU: Normale Sätze ECHT aus der Datenbank löschen ---
        if (match.Sets != null && match.Sets.Any())
        {
            _db.Sets.RemoveRange(match.Sets);
        }

        // Alles sauber zurücksetzen
        match.Sets = null;
        match.IsWalkover = false;
        match.WalkoverTeamId = null;
        match.WinnerTeamId = null;

        await _db.SaveChangesAsync();

        Message = "Das Match wurde erfolgreich zurückgesetzt und ist wieder offen.";
        // Passe den Redirect an deine Route an, falls nötig
        return RedirectToPage();
    }

    #endregion

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }

    public IActionResult OnPostCreateBracket()
    {
        InitValues();
        if (!_knownBrackets.Contains(SelectedSize)) return RedirectToPage();

        UpdateBracket(SelectedSize);
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
        double baseT = (double)size / 2;
        double baseC = (double)size / 2;
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

        _db.SaveChanges();
        return RedirectToPage();
    }

    public IActionResult OnPostSaveNewDate(string newDate, string newTime)
    {
        InitValues();

        var selectedCompetition = _db.Competitions.Single(x => x.Id == SelectedCompetition!.Id);

        var date = DateOnly.Parse(newDate);
        var time = TimeOnly.Parse(newTime);

        selectedCompetition.RegistrationUntil = date.ToDateTime(time);

        _db.SaveChanges();
        return RedirectToPage(new { Message = "Neues Datum wurde gespeichert" });
    }

    public async Task<IActionResult> OnPostGiveWalkoverAsync(int matchId)
    {
        // Lade das Match inklusive der Teams und deren Spieler
        var match = await _db.Matches
            .Include(m => m.Team1).ThenInclude(t => t.Players).ThenInclude(teamPlayer => teamPlayer.Player)
            .Include(m => m.Team2).ThenInclude(t => t.Players).ThenInclude(teamPlayer => teamPlayer.Player)
            .Include(match => match.Group)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        // Gehört der User zu Team 1 oder Team 2?
        bool isTeam1 = match.Team1 != null && match.Team1.Players.Any(p => p.Player.IdentityUserId == currentUser.Id);
        bool isTeam2 = match.Team2 != null && match.Team2.Players.Any(p => p.Player.IdentityUserId == currentUser.Id);

        if (!isTeam1 && !isTeam2) return Forbid(); // User spielt in diesem Match gar nicht mit

        // Walkover setzen
        match.IsWalkover = true;

        // Wer hat aufgegeben und wer hat gewonnen?
        if (match is { Team1: not null, Team2: not null })
        {
            match.WalkoverTeamId = isTeam1 ? match.Team1.Id : match.Team2.Id;
            match.Winner = isTeam1 ? match.Team2 : match.Team1;
        }

        await _db.SaveChangesAsync();

        Message = "Du hast das Match aufgegeben. Der Sieg geht per w.o. an die Gegner.";
        return RedirectToPage(new { Message });
    }

// 2. Für den Admin (wählt aus, welches Team W.O. gegeben hat)
    public async Task<IActionResult> OnPostAdminWalkoverAsync(int matchId, int walkoverTeamId)
    {
        var match = await _db.Matches
            .Include(m => m.Team1).ThenInclude(t => t.Players).ThenInclude(teamPlayer => teamPlayer.Player)
            .Include(m => m.Team2).ThenInclude(t => t.Players).ThenInclude(teamPlayer => teamPlayer.Player)
            .Include(match => match.Group)
            .FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) return NotFound();

        match.IsWalkover = true;
        match.WalkoverTeamId = walkoverTeamId;

        // Der Sieger ist das Team, das NICHT aufgegeben hat
        match.Winner = match.Team1 != null && match.Team1.Id == walkoverTeamId ? match.Team2 : match.Team1;

        await _db.SaveChangesAsync();

        Message = "Match wurde durch Admin als w.o. gewertet.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostWithdrawPlayerAsync(int playerId, int competitionId)
    {
        return await WithDrawPlayer(playerId, competitionId);
    }

    public async Task<IActionResult> OnPostSelfWithdrawAsync(int selectedCompetition)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var player = _db.Players.Single(x => x.IdentityUserId == currentUser.Id);

        return await WithDrawPlayer(player.Id, selectedCompetition);
    }

    private async Task<IActionResult> WithDrawPlayer(int playerId, int competitionId)
    {
        var userTeams = await _db.Teams
            .Include(t => t.Players)
            .Where(t => t.CompetitionId == competitionId && t.Players.Any(p => p.PlayerId == playerId))
            .ToListAsync();

        var teamIds = userTeams.Select(t => t.Id).ToList();

        if (!teamIds.Any())
        {
            Message = "Spieler ist in keinen Teams dieses Bewerbs.";
            return RedirectToPage(new { selectedCompetitionId = competitionId });
        }

        // 2. Finde alle noch NICHT GESPIELTEN Matches für diese Teams in diesem Bewerb
        // (Wir holen auch Team1 und Team2 dazu, falls wir sie gleich brauchen)
        var unplayedMatches = await _db.Matches
            .Include(x => x.Winner)
            .Include(x => x.Team1)
            .Include(x => x.Team2)
            .Where(m => teamIds.Contains(m.Team1.Id) || teamIds.Contains(m.Team2.Id))
            .ToListAsync();

        // 3. Setze alle diese Matches automatisch auf w.o.
        foreach (var match in unplayedMatches)
        {
            match.IsWalkover = true;

            // Welches Team hat das W.O. verursacht? (Das Team des abgemeldeten Spielers)
            bool team1Withdrew = teamIds.Contains(match.Team1.Id);

            match.WalkoverTeamId = team1Withdrew ? match.Team1.Id : match.Team2.Id;
            match.WinnerTeamId = team1Withdrew ? match.Team2.Id : match.Team1.Id;
        }

        // 4. (Optional) Markiere den Spieler in der Anmeldeliste als abgemeldet
        // Falls du die HasWithdrawn-Spalte schon in 'RegisteredCompetitionPlayers' hast:

        var registration = await _db.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.PlayerId == playerId);
        if (registration != null)
        {
            registration.HasWithdrawn = true;
        }

        await _db.SaveChangesAsync();
        return RedirectToPage(new
        {
            Message =
                "Spieler wurde abgemeldet. Alle seine offenen Spiele wurden automatisch als w.o. für die Gegner gewertet."
        });
    }

    private List<GroupTableEntry> CalculateGroupTable(IEnumerable<GroupTeam> groupTeams,
        IEnumerable<Match> groupMatches)
    {
        var table = new List<GroupTableEntry>();

        foreach (var groupTeam in groupTeams)
        {
            var entry = new GroupTableEntry { GroupTeam = groupTeam };

            // HIER filtern wir die Matches für das aktuelle Team (das ist "teamMatches")
            var teamMatches = groupMatches.Where(m =>
                m.Team1.Id == groupTeam.TeamId || m.Team2.Id == groupTeam.TeamId).ToList();

            entry.MatchesPlayed = teamMatches.Count;

            // Jetzt werten wir diese Matches aus
            foreach (var match in teamMatches)
            {
                // Greife sicher auf die ID zu
                int team1Id = match.Team1.Id;
                bool isTeam1 = team1Id == groupTeam.TeamId;

                if (match.IsWalkover)
                {
                    // LOGIK FÜR W.O. MATCHES
                    if (match.IsWalkover)
                    {
                        // LOGIK FÜR W.O. MATCHES
                        // Der Gewinner ist das Team, das in diesem Match NICHT w.o. gegeben hat!
                        bool isWinner = match.WalkoverTeamId != groupTeam.TeamId;

                        if (isWinner)
                        {
                            entry.Points++; // 1 Siegpunkt
                            entry.SetsWon += 2; // 2 Sätze
                            entry.GamesWon += 12; // 12 Games
                        }
                        else
                        {
                            entry.SetsLost += 2;
                            entry.GamesLost += 12;
                        }
                    }
                }
                else if (match.Sets != null && match.Sets.Any())
                {
                    // LOGIK FÜR NORMALE MATCHES
                    int setsWonHere = 0;
                    int setsLostHere = 0;

                    foreach (var set in match.Sets)
                    {
                        int myGames = isTeam1 ? set.Player1GamesWon : set.Player2GamesWon;
                        int oppGames = isTeam1 ? set.Player2GamesWon : set.Player1GamesWon;

                        entry.GamesWon += myGames;
                        entry.GamesLost += oppGames;

                        if (myGames > oppGames) setsWonHere++;
                        else if (oppGames > myGames) setsLostHere++;
                    }

                    entry.SetsWon += setsWonHere;
                    entry.SetsLost += setsLostHere;

                    // Hat das Team das normale Match gewonnen? Dann gibt es den Punkt!
                    if (setsWonHere > setsLostHere)
                    {
                        entry.Points++;
                    }
                }
            }

            table.Add(entry);
        }

        // Die Tabelle nach Punkten, dann Satzdifferenz, dann Gamedifferenz sortieren
        return table
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.SetDifference)
            .ThenByDescending(e => e.GameDifference)
            .ToList();
    }
}