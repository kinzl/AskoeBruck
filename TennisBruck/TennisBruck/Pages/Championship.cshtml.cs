using Microsoft.AspNetCore.Identity;
using Group = TennisDb.Group;
using Match = TennisDb.Match;

namespace TennisBruck.Pages;

[Authorize]
[BindProperties]
public class Championship(
    CurrentPlayerService currentPlayerService,
    TennisContext db,
    UserManager<IdentityUser> userManager)
    : PageModel
{
    public bool IsRegistered { get; set; }
    public required Player CurrentPlayer { get; set; }

    public required List<Competition> Competitions { get; set; }
    public Competition? SelectedCompetition { get; set; }
    public List<TournamentRegistration> RegisteredCompetitionPlayers { get; set; } = [];
    public List<Group> Groups { get; set; } = [];
    public required List<Match> PersonalMatches { get; set; }
    public required List<Match> AllMatches { get; set; }
    public required List<Team> RegisteredTeams { get; set; }
    [BindProperty] public int SelectedSize { get; set; }
    [BindProperty] public string PhaseName { get; set; } = "A-Bewerb";

    [BindProperty] public List<BracketInput> Inputs { get; set; } = [];

    public List<KnockoutMatch> Matches { get; set; } = [];

    private readonly List<int> _knownBrackets = [2, 4, 8, 16, 32];
    public string? Message { get; set; }
    public required List<Player> UnregisteredPlayers { get; set; }
    public Dictionary<int, List<GroupTableEntry>> GroupTables { get; set; } = new();

    public IActionResult OnGet(string? message)
    {
        return InitValues(message);
    }

    private IActionResult InitValues(string? message = null)
    {
        int? selectedCompetitionId = int.Parse(HttpContext.Session.GetString("selectedCompetitionId") ?? "0");
        CurrentPlayer = currentPlayerService.GetCurrentUser()!;
        Message = message;
        Competitions = db.Competitions.ToList();

        PersonalMatches = db.Matches
            .Include(m => m.Group.Competition)
            .Include(m => m.Team1).ThenInclude(t => t.TeamPlayers).ThenInclude(tp => tp.Player)
            .Include(m => m.Team2).ThenInclude(t => t.TeamPlayers).ThenInclude(tp => tp.Player)
            .Include(m => m.Sets)
            .Where(m => m.Team1.TeamPlayers.Any(tp => tp.PlayerId == CurrentPlayer.Id) ||
                        m.Team2.TeamPlayers.Any(tp => tp.PlayerId == CurrentPlayer.Id))
            .ToList();

        if (selectedCompetitionId != 0)
        {
            SelectedCompetition = Competitions.FirstOrDefault(c => c.Id == selectedCompetitionId);

            Matches = db.KnockoutMatch
                .Include(x => x.Team1).ThenInclude(t => t.TeamPlayers).ThenInclude(tp => tp.Player)
                .Include(x => x.Team2).ThenInclude(t => t.TeamPlayers).ThenInclude(tp => tp.Player)
                .Where(x => x.CompetitionId == selectedCompetitionId)
                .ToList();

            IsRegistered = db.TournamentRegistrations
                .Where(x => x.Competition.Id == SelectedCompetition!.Id)
                .Any(x => x.Player.Id == CurrentPlayer.Id);


            RegisteredTeams = db.Teams
                .Include(x => x.TeamPlayers)
                .ThenInclude(x => x.Player)
                .Where(x => x.Competition.Id == SelectedCompetition!.Id)
                .ToList();

            RegisteredCompetitionPlayers = db.TournamentRegistrations
                .Include(x => x.Competition)
                .Include(x => x.Player)
                .Where(x => x.Competition.Id == selectedCompetitionId)
                .ToList();

            UnregisteredPlayers = db.Players
                .Where(p => p.TournamentRegistrations.All(r => r.CompetitionId != SelectedCompetition!.Id))
                .ToList();

            Groups = db.Groups
                .Where(g => g.Competition.Id == selectedCompetitionId)
                .Include(g => g.GroupTeams)
                .ThenInclude(gt => gt.Team)
                .ThenInclude(t => t.TeamPlayers)
                .ThenInclude(tp => tp.Player)
                .Include(g => g.Competition)
                .ThenInclude(c => c.Teams)
                .ToList();

            AllMatches = db.Matches
                .Include(x => x.Group)
                .ThenInclude(x => x.Competition)
                .Include(x => x.Team1)
                .Include(x => x.Team2)
                .Include(x => x.Sets)
                .Where(x => x.Group != null && x.Group.Competition.Id == SelectedCompetition!.Id)
                .ToList();

            AllMatches.AddRange(db.KnockoutMatch
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

        return Page();
    }

    #region CRUD Competition

    public IActionResult OnPostDeleteCompetition(int competitionId)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        var competition = db.Competitions.Find(competitionId);
        if (competition == null) return RedirectToPage(new { Message = "Ein Fehler ist aufgetreten" });

        db.Competitions.Remove(competition);
        db.SaveChanges();
        HttpContext.Session.SetString("selectedCompetitionId", "0");
        return RedirectToPage(new { Message = "Bewerb wurde gelöscht" });
    }

    public IActionResult OnPostCreateCompetition(string competitionName, bool? isSingle)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        if (competitionName.IsNullOrEmpty() || !isSingle.HasValue)
            return RedirectToPage(new { Message = "Bitte geben Sie einen Namen ein oder Wählen Sie einen Bewerb" });
        db.Competitions.Add(new Competition
        {
            Name = competitionName,
            IsSingle = isSingle.Value,
            RegistrationUntil = DateTime.Now.AddDays(14),
            Teams = []
        });
        db.SaveChanges();
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
        db.TournamentRegistrations.Add(new TournamentRegistration()
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
                TeamPlayers = new List<TeamPlayer>
                {
                    new() { PlayerId = playerId ?? CurrentPlayer.Id }
                }
            };
            db.Teams.Add(team);
        }

        db.SaveChanges();
        return RedirectToPage(new { Message = $"Du hast dich beim Bewerb {SelectedCompetition.Name} angemeldet" });
    }

    public IActionResult OnPostUnregister()
    {
        InitValues();
        var registeredPlayer = db.TournamentRegistrations
            .SingleOrDefault(x => x.Player.Id == CurrentPlayer.Id && x.Competition.Id == SelectedCompetition!.Id);

        var teamPlayer = db.TeamPlayer.SingleOrDefault(x =>
            x.Player.Id == CurrentPlayer.Id && x.Team.Competition.Id == SelectedCompetition!.Id);

        var team = db.Teams.SingleOrDefault(x =>
            x.Competition.Id == SelectedCompetition!.Id && x.TeamPlayers.Any(y => y.Player.Id == CurrentPlayer.Id));

        if (registeredPlayer != null) db.TournamentRegistrations.Remove(registeredPlayer);
        if (teamPlayer != null) db.TeamPlayer.Remove(teamPlayer);
        if (team != null) db.Teams.Remove(team);


        var groupTeam = db.GroupTeams.SingleOrDefault(x =>
            x.Group.Competition.Id == SelectedCompetition!.Id &&
            x.Team.TeamPlayers.Any(y => y.Player.Id == CurrentPlayer.Id));
        if (groupTeam != null)
        {
            db.GroupTeams.Remove(groupTeam);
        }

        db.SaveChanges();
        return RedirectToPage(new { Message = $"Du hast dich vom Bewerb {SelectedCompetition!.Name} abgemeldet" });
    }

    public IActionResult OnPostDeleteTeam(int teamId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        InitValues();

        var team = db.Teams.Include(t => t.TeamPlayers).SingleOrDefault(t => t.Id == teamId);
        if (team != null)
        {
            // Remove regular matches involving this team
            var matches = db.Matches.Where(m => m.Team1.Id == teamId || m.Team2.Id == teamId).ToList();
            db.Matches.RemoveRange(matches);
            
            // Unlink from knockout matches
            var knockoutMatches = db.KnockoutMatch.Where(m => (m.Team1 != null && m.Team1.Id == teamId) || (m.Team2 != null && m.Team2.Id == teamId)).ToList();
            foreach (var km in knockoutMatches)
            {
                if (km.Team1?.Id == teamId) km.Team1 = null;
                if (km.Team2?.Id == teamId) km.Team2 = null;
            }
            
            // Remove from groups
            var groupTeams = db.GroupTeams.Where(gt => gt.TeamId == teamId).ToList();
            db.GroupTeams.RemoveRange(groupTeams);

            db.Teams.Remove(team);

            // If it's a singles competition, also remove the tournament registration for the player
            if (SelectedCompetition != null && SelectedCompetition.IsSingle)
            {
                var playerId = team.TeamPlayers.First().PlayerId;
                var reg = db.TournamentRegistrations.SingleOrDefault(tr => tr.PlayerId == playerId && tr.CompetitionId == SelectedCompetition.Id);
                if (reg != null) db.TournamentRegistrations.Remove(reg);
            }

            db.SaveChanges();
            return RedirectToPage(new { Message = "Team wurde erfolgreich gelöscht." });
        }
        return RedirectToPage(new { Message = "Team nicht gefunden." });
    }

    #endregion

    #region Group Management

    public IActionResult OnPostCreateGroup()
    {
        if (!User.IsInRole("Admin")) return Forbid();
        InitValues();
        db.Groups.Add(new Group
        {
            Competition = SelectedCompetition!,
            MaxAmount = 1,
            GroupName = "Gruppe "
        });
        db.SaveChanges();

        var groups = db.Groups.Where(x => x.Competition.Id == SelectedCompetition!.Id).ToList();

        for (int i = 0; i < groups.Count; i++)
        {
            groups[i].GroupName = $"Gruppe {(char)(i + 65)}";
        }

        db.SaveChanges();

        return RedirectToPage(new { Message = "Neue Gruppe erstellt" });
    }

    public IActionResult OnPostDeleteGroup(int groupId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        var selectedGroup = db.Groups.Single(x => x.Id == groupId);
        db.Groups.Remove(selectedGroup);
        db.SaveChanges();
        return RedirectToPage(new { Message = $"Gruppe {selectedGroup.GroupName} gelöscht" });
    }

    public IActionResult OnPostSaveGroups()
    {
        if (!User.IsInRole("Admin")) return Forbid();
        InitValues();

        // Delete old matches
        var removedMatches = db.Matches
            .Where(m => m.Group.Competition.Id == SelectedCompetition!.Id)
            .ToList();

        db.Matches.RemoveRange(removedMatches);
        db.SaveChanges();

        // load groups with teams
        var groups = db.Groups
            .Where(g => g.Competition.Id == SelectedCompetition!.Id)
            .Include(g => g.GroupTeams)
            .ThenInclude(gt => gt.Team)
            .ThenInclude(t => t.TeamPlayers) // TeamPlayer
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
                    db.Matches.Add(new Match
                    {
                        Group = group,
                        Team1 = teams[i],
                        Team2 = teams[j]
                    });
                }
            }
        }

        db.SaveChanges();

        return RedirectToPage(new { Message = "Spiele wurden erstellt" });
    }

    public IActionResult OnPostIncreaseGroupSize(int groupId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        var group = db.Groups.Single(x => x.Id == groupId);
        group.MaxAmount++;
        db.SaveChanges();
        return RedirectToPage(new { Message = $"{group.GroupName} vergrößert" });
    }

    public IActionResult OnPostDecreaseGroupSize(int groupId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        var group = db.Groups.Single(x => x.Id == groupId);
        if (group.MaxAmount == 1) return RedirectToPage();
        group.MaxAmount--;
        db.SaveChanges();
        return RedirectToPage(new { Message = $"{group.GroupName} verkleinert" });
    }

    #endregion

    public IActionResult OnPostAddSinglePlayer(int teamId, int groupId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        var group = db.Groups
            .Include(g => g.Competition)
            .Single(g => g.Id == groupId);

        // Team des Spielers im selben Bewerb suchen
        var existingGroupTeam = db.GroupTeams
            .Include(gt => gt.Group)
            .ThenInclude(g => g.Competition)
            .Include(gt => gt.Team)
            .ThenInclude(t => t.TeamPlayers)
            .SingleOrDefault(gt =>
                gt.Team.Id == teamId &&
                gt.Group.Competition.Id == group.Competition.Id
            );

        if (existingGroupTeam != null && existingGroupTeam.GroupId == groupId)
            return RedirectToPage(new { Message = "Spieler ist bereits in dieser Gruppe" });


        if (existingGroupTeam != null)
        {
            existingGroupTeam.GroupId = groupId;
            db.SaveChanges();
            return RedirectToPage();
        }

        var team = db.Teams.SingleOrDefault(x => x.Id == teamId);
        if (team == null) return RedirectToPage();

        var groupTeam = new GroupTeam
        {
            GroupId = groupId,
            TeamId = team.Id
        };
        db.GroupTeams.Add(groupTeam);
        db.SaveChanges();

        return RedirectToPage(new { Message = $"{team} in {group.GroupName} hinzugefügt" });
    }

    public IActionResult OnPostRemoveTeamFromGroup(int groupId, int teamId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        var groupTeam = db.GroupTeams
            .Include(gt => gt.Team)
            .ThenInclude(t => t.TeamPlayers).Include(groupTeam => groupTeam.Group)
            .Single(gt => gt.GroupId == groupId && gt.TeamId == teamId);

        db.GroupTeams.Remove(groupTeam);

        db.SaveChanges();
        return RedirectToPage(new
            { Message = $"{groupTeam.Team} von {groupTeam.Group.GroupName} entfernt" });
    }

    #region Match Management

    public IActionResult OnPostSaveMatch(string score, int matchId)
    {
        try
        {
            var currentUser = currentPlayerService.GetCurrentUser();
            if (currentUser == null) return Unauthorized();

            int setsWonPlayer1 = 0;
            int setsWonPlayer2 = 0;
            var match = db.Matches
                .Include(x => x.Sets)
                .Include(x => x.Team1).ThenInclude(t => t.TeamPlayers).ThenInclude(tp => tp.Player)
                .Include(x => x.Team2).ThenInclude(t => t.TeamPlayers).ThenInclude(tp => tp.Player)
                .Include(x => x.Group)
                .Single(x => x.Id == matchId);

            bool isTeam1 = match.Team1 != null && match.Team1.TeamPlayers.Any(p => p.Player.Id == currentUser.Id);
            bool isTeam2 = match.Team2 != null && match.Team2.TeamPlayers.Any(p => p.Player.Id == currentUser.Id);
            if (!isTeam1 && !isTeam2 && !User.IsInRole("Admin")) return Forbid();
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

            db.SaveChanges();
        }
        catch (Exception)
        {
            return RedirectToPage(new
                { Message = "Fehler beim Speichern des Spiels (Falsche eingabe des Spielstandes?)" });
        }

        return RedirectToPage(new
            { Message = "Spiel wurde gespeichert" });
    }

    public async Task<IActionResult> OnPostDeleteMatchAsync(int matchId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        // WICHTIG: .Include(m => m.Sets) hinzufügen, damit er die Sätze auch findet!
        var match = await db.Matches
            .Include(m => m.Sets)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null) return NotFound();

        bool isAdmin = User.IsInRole("Admin");
        if (match.IsWalkover && !isAdmin)
        {
            Message = "Ein Walkover kann nur vom Admin rückgängig gemacht werden.";
            return RedirectToPage();
        }

        if (match.Sets != null && match.Sets.Any()) db.Sets.RemoveRange(match.Sets);

        match.Sets = null;
        match.IsWalkover = false;
        match.WalkoverTeamId = null;
        match.WinnerTeamId = null;

        await db.SaveChangesAsync();

        return RedirectToPage(new { Message = "Das Match wurde erfolgreich zurückgesetzt und ist wieder offen." });
    }

    #endregion

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }

    public IActionResult OnPostCreateBracket()
    {
        if (!User.IsInRole("Admin")) return Forbid();
        InitValues();
        if (!_knownBrackets.Contains(SelectedSize)) return RedirectToPage();

        // Default value checks
        if (string.IsNullOrWhiteSpace(PhaseName)) PhaseName = "A-Bewerb";

        TempData["ActivePhase"] = PhaseName.Trim();

        UpdateBracket(SelectedSize, PhaseName.Trim());
        return RedirectToPage();
    }

    public IActionResult OnPostSwitchPhase(string phaseName)
    {
        HttpContext.Session.SetString("ActivePhase", phaseName);
        return RedirectToPage();
    }

    private void UpdateBracket(int size, string phaseName)
    {
        db.KnockoutMatch.Where(k => k.CompetitionId == SelectedCompetition!.Id && k.PhaseName == phaseName)
            .ExecuteDelete();
        db.SaveChanges();
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

            db.KnockoutMatch.Add(new KnockoutMatch()
            {
                CompetitionId = SelectedCompetition!.Id,
                PhaseName = phaseName,
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

        db.SaveChanges();
    }

    public IActionResult OnPostApplyUserInputs()
    {
        if (!User.IsInRole("Admin")) return Forbid();
        InitValues();

        foreach (var match in Matches)
        {
            var input = Inputs.FirstOrDefault(i => i.MatchId == match.Id);

            if (input != null)
            {
                match.Team1 = db.Teams
                    .Include(t => t.TeamPlayers)
                    .ThenInclude(tp => tp.Player)
                    .SingleOrDefault(t => t.Id == input.Team1Id);

                match.Team2 = db.Teams
                    .Include(t => t.TeamPlayers)
                    .ThenInclude(tp => tp.Player)
                    .SingleOrDefault(t => t.Id == input.Team2Id);
            }
        }

        db.SaveChanges();
        return RedirectToPage(new { Message = "Zuteilung wurde gespeichert" });
    }

    public IActionResult OnPostSavePairs(List<PlayerCompetitionPairs> pairs)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        InitValues();

        foreach (var pair in pairs)
        {
            if (!pair.SinglePlayerId.HasValue || !pair.DoublePlayerId.HasValue)
                continue;

            int player1Id = pair.SinglePlayerId.Value;
            int player2Id = pair.DoublePlayerId.Value;

            // Prüfen, ob Spieler schon in einem Team in dieser Competition sind
            bool exists = db.Teams
                .Include(t => t.TeamPlayers)
                .Any(t => t.TeamPlayers.Any(tp => tp.PlayerId == player1Id || tp.PlayerId == player2Id)
                          && t.CompetitionId == SelectedCompetition!.Id);

            if (exists) continue;

            // Neues Doppel-Team erstellen
            var team = new Team
            {
                CompetitionId = SelectedCompetition!.Id,
                TeamPlayers = new List<TeamPlayer>
                {
                    new() { PlayerId = player1Id },
                    new() { PlayerId = player2Id }
                }
            };

            db.Teams.Add(team);
        }

        db.SaveChanges();
        return RedirectToPage(new { Message = "Teams wurden gespeichert" });
    }

    public IActionResult OnPostSaveNewDate(string newDate, string newTime)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        InitValues();

        var selectedCompetition = db.Competitions.Single(x => x.Id == SelectedCompetition!.Id);

        var date = DateOnly.Parse(newDate);
        var time = TimeOnly.Parse(newTime);

        selectedCompetition.RegistrationUntil = date.ToDateTime(time);

        db.SaveChanges();
        return RedirectToPage(new { Message = "Neues Datum wurde gespeichert" });
    }

    public async Task<IActionResult> OnPostGiveWalkoverAsync(int matchId)
    {
        var match = await db.Matches
            .Include(m => m.Team1).ThenInclude(t => t.TeamPlayers).ThenInclude(teamPlayer => teamPlayer.Player)
            .Include(m => m.Team2).ThenInclude(t => t.TeamPlayers).ThenInclude(teamPlayer => teamPlayer.Player)
            .Include(match => match.Group)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null) return NotFound();

        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        bool isTeam1 = match.Team1 != null &&
                       match.Team1.TeamPlayers.Any(p => p.Player.IdentityUserId == currentUser.Id);
        bool isTeam2 = match.Team2 != null &&
                       match.Team2.TeamPlayers.Any(p => p.Player.IdentityUserId == currentUser.Id);

        if (!isTeam1 && !isTeam2) return Forbid();

        match.IsWalkover = true;

        if (match is { Team1: not null, Team2: not null })
        {
            match.WalkoverTeamId = isTeam1 ? match.Team1.Id : match.Team2.Id;
            match.Winner = isTeam1 ? match.Team2 : match.Team1;
        }

        await db.SaveChangesAsync();

        return RedirectToPage(new { Message = "Du hast das Match aufgegeben. Der Sieg geht per w.o. an die Gegner." });
    }

    public async Task<IActionResult> OnPostAdminWalkoverAsync(int matchId, int walkoverTeamId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        var match = await db.Matches
            .Include(m => m.Team1).ThenInclude(t => t.TeamPlayers).ThenInclude(teamPlayer => teamPlayer.Player)
            .Include(m => m.Team2).ThenInclude(t => t.TeamPlayers).ThenInclude(teamPlayer => teamPlayer.Player)
            .Include(match => match.Group)
            .FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) return NotFound();

        match.IsWalkover = true;
        match.WalkoverTeamId = walkoverTeamId;

        match.Winner = match.Team1 != null && match.Team1.Id == walkoverTeamId ? match.Team2 : match.Team1;

        await db.SaveChangesAsync();

        return RedirectToPage(new { Message = "Match wurde durch Admin als w.o. gewertet." });
    }

    public async Task<IActionResult> OnPostWithdrawPlayerAsync(int playerId, int competitionId)
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var playerToWithdraw = await db.Players.FindAsync(playerId);
        if (playerToWithdraw == null) return NotFound();

        if (playerToWithdraw.IdentityUserId != currentUser.Id && !User.IsInRole("Admin")) return Forbid();

        return await WithDrawPlayer(playerId, competitionId);
    }

    public async Task<IActionResult> OnPostSelfWithdrawAsync(int selectedCompetition)
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var player = db.Players.Single(x => x.IdentityUserId == currentUser.Id);

        return await WithDrawPlayer(player.Id, selectedCompetition);
    }

    private async Task<IActionResult> WithDrawPlayer(int playerId, int competitionId)
    {
        var userTeams = await db.Teams
            .Include(t => t.TeamPlayers)
            .Where(t => t.CompetitionId == competitionId && t.TeamPlayers.Any(p => p.PlayerId == playerId))
            .ToListAsync();

        var teamIds = userTeams.Select(t => t.Id).ToList();

        if (!teamIds.Any()) return RedirectToPage(new { Message = "Spieler ist in keinen Teams dieses Bewerbs." });

        var unplayedMatches = await db.Matches
            .Include(x => x.Winner)
            .Include(x => x.Team1)
            .Include(x => x.Team2)
            .Where(m => teamIds.Contains(m.Team1.Id) || teamIds.Contains(m.Team2.Id))
            .ToListAsync();

        foreach (var match in unplayedMatches)
        {
            match.IsWalkover = true;
            bool team1Withdrew = teamIds.Contains(match.Team1.Id);

            match.WalkoverTeamId = team1Withdrew ? match.Team1.Id : match.Team2.Id;
            match.WinnerTeamId = team1Withdrew ? match.Team2.Id : match.Team1.Id;
        }

        var registration = await db.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.PlayerId == playerId);
        if (registration != null) registration.HasWithdrawn = true;

        await db.SaveChangesAsync();
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

            var teamMatches = groupMatches.Where(m =>
                m.Team1.Id == groupTeam.TeamId || m.Team2.Id == groupTeam.TeamId).ToList();

            entry.MatchesPlayed = teamMatches.Count;

            foreach (var match in teamMatches)
            {
                int team1Id = match.Team1.Id;
                bool isTeam1 = team1Id == groupTeam.TeamId;

                if (match.IsWalkover)
                {
                    bool isWinner = match.WalkoverTeamId != groupTeam.TeamId;

                    if (isWinner)
                    {
                        entry.Points++;
                        entry.SetsWon += 2;
                        entry.GamesWon += 12;
                    }
                    else
                    {
                        entry.SetsLost += 2;
                        entry.GamesLost += 12;
                    }
                }
                else if (match.Sets != null && match.Sets.Any())
                {
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

                    if (setsWonHere > setsLostHere)
                    {
                        entry.Points++;
                    }
                }
            }

            table.Add(entry);
        }

        return table
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.SetDifference)
            .ThenByDescending(e => e.GameDifference)
            .ToList();
    }

    public async Task<IActionResult> OnPostGenerateGroupsAsync(int competitionId, int targetGroupSize)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        if (targetGroupSize < 2) targetGroupSize = 2;

        var players = await db.TournamentRegistrations
            .Where(p => p.CompetitionId == competitionId)
            .ToListAsync();

        if (!players.Any()) return RedirectToPage(new { id = competitionId });

        var oldGroups = await db.Groups
            .Include(g => g.GroupTeams)
            .Include(g => g.Matches)
            .Where(g => g.CompetitionId == competitionId)
            .ToListAsync();

        db.Matches.RemoveRange(oldGroups.SelectMany(x => x.Matches).ToList());
        db.GroupTeams.RemoveRange(oldGroups.SelectMany(gt => gt.GroupTeams).ToList());
        db.Groups.RemoveRange(oldGroups);

        await db.SaveChangesAsync();

        int numberOfGroups = (int)Math.Ceiling((double)players.Count / targetGroupSize);

        var newGroups = new List<Group>();
        for (int i = 0; i < numberOfGroups; i++)
        {
            newGroups.Add(new Group
            {
                CompetitionId = competitionId,
                GroupName = $"Gruppe {(char)('A' + i)}",
                MaxAmount = targetGroupSize,
                GroupTeams = []
            });
        }

        var random = new Random();
        var shuffledPlayers = db.Teams.Where(x => x.CompetitionId == competitionId).ToList();

        shuffledPlayers = shuffledPlayers.OrderBy(_ => random.Next()).ToList();

        for (int i = 0; i < shuffledPlayers.Count; i++)
        {
            int groupIndex = i % numberOfGroups;

            db.GroupTeams.Add(new GroupTeam
            {
                Group = newGroups[groupIndex],
                TeamId = shuffledPlayers[i].Id
            });
        }

        db.Groups.AddRange(newGroups);
        await db.SaveChangesAsync();

        return RedirectToPage(new { Message = "Gruppen wurden erstellt" });
    }

    public async Task<IActionResult> OnPostGeneratePairsAsync(int championshipId)
    {
        var players = await db.TournamentRegistrations
            .Where(p => p.CompetitionId == championshipId)
            .Include(x => x.Player)
            .ToListAsync();

        if (players.Count < 2) return RedirectToPage(new { id = championshipId });

        var oldTeams = await db.Teams.Where(x => x.CompetitionId == championshipId).ToListAsync();
        var oldGroupTeams = await db.GroupTeams
            .Where(t => t.Group.CompetitionId == championshipId)
            .ToListAsync();

        db.GroupTeams.RemoveRange(oldGroupTeams);
        db.Teams.RemoveRange(oldTeams);
        await db.SaveChangesAsync();

        var rng = new Random();
        var shuffledPlayers = players.OrderBy(_ => rng.Next()).ToList();

        List<Team> newTeams = [];

        for (int i = 0; i < shuffledPlayers.Count - 1; i += 2)
        {
            var player1 = shuffledPlayers[i];
            var player2 = shuffledPlayers[i + 1];

            var team = new Team
            {
                CompetitionId = championshipId,
                TeamPlayers = new List<TeamPlayer>
                    { new() { Player = player1.Player }, new() { Player = player2.Player } }
            };
            newTeams.Add(team);
        }

        db.Teams.AddRange(newTeams);
        await db.SaveChangesAsync();

        return RedirectToPage(new { Message = "Paare wurden generiert" });
    }
}