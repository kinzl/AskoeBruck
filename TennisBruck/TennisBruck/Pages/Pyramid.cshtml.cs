using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TennisBruck.Services;
using TennisDb;

namespace TennisBruck.Pages;

public class PyramidModel(TennisContext db, CurrentPlayerService currentPlayerService, IEmailSender emailSender) : PageModel
{
    public List<Competition> PyramidCompetitions { get; set; } = [];
    public Competition? SelectedCompetition { get; set; }
    public List<PyramidLevel> PyramidLevels { get; set; } = [];
    public List<PyramidChallenge> ActiveChallenges { get; set; } = [];
    public List<PyramidChallenge> PastChallenges { get; set; } = [];
    public List<Player> AllPlayers { get; set; } = [];
    public List<Player> AvailablePartners { get; set; } = [];
    public List<Player> UnregisteredPlayers { get; set; } = [];
    public List<TournamentRegistration> TournamentRegistrations { get; set; } = [];
    public List<Team> RegisteredTeams { get; set; } = [];
    public Player? CurrentPlayer { get; private set; }
    public Team? MyTeam { get; private set; }
    public PyramidRank? MyRank { get; private set; }
    public bool IsCurrentUserInPyramid => MyRank != null;
    public bool IsRegistrationOpen => SelectedCompetition != null && DateTime.Now <= SelectedCompetition.RegistrationUntil;
    public bool IsCurrentUserRegisteredInPool => CurrentPlayer != null && TournamentRegistrations.Any(r => r.PlayerId == CurrentPlayer.Id && !r.HasWithdrawn);

    [BindProperty] public string? Message { get; set; }
    [BindProperty] public bool IsError { get; set; }

    public class PyramidLevel
    {
        public int LevelNumber { get; set; }
        public List<PyramidPositionNode> Nodes { get; set; } = [];
    }

    public class PyramidPositionNode
    {
        public required PyramidRank PyramidRank { get; set; }
        public PyramidChallenge? ActiveChallenge { get; set; }
        public bool IsMyTeam { get; set; }
        public bool CanBeChallengedByCurrentUser { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? competitionId, string? message, bool isError = false)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        Message = message;
        IsError = isError;

        AllPlayers = await db.Players.OrderBy(p => p.Lastname).ThenBy(p => p.Firstname).ToListAsync();

        PyramidCompetitions = await db.Competitions
            .Where(c => c.IsPyramid)
            .OrderBy(c => c.Name)
            .ToListAsync();

        if (!PyramidCompetitions.Any())
        {
            return Page();
        }

        int targetCompId = competitionId ?? HttpContext.Session.GetInt32("SelectedPyramidCompId") ?? PyramidCompetitions.First().Id;
        SelectedCompetition = PyramidCompetitions.FirstOrDefault(c => c.Id == targetCompId) ?? PyramidCompetitions.First();
        HttpContext.Session.SetInt32("SelectedPyramidCompId", SelectedCompetition.Id);

        var rawRegistrations = await db.TournamentRegistrations
            .Include(tr => tr.Player)
            .Where(tr => tr.CompetitionId == SelectedCompetition.Id && !tr.HasWithdrawn)
            .ToListAsync();

        TournamentRegistrations = rawRegistrations
            .DistinctBy(tr => tr.PlayerId)
            .ToList();

        // Fetch ranks for selected competition
        var ranks = await db.PyramidRanks
            .Include(r => r.Team)
                .ThenInclude(t => t.TeamPlayers)
                    .ThenInclude(tp => tp.Player)
            .Where(r => r.CompetitionId == SelectedCompetition.Id)
            .OrderBy(r => r.Rank)
            .ToListAsync();

        RegisteredTeams = ranks.Select(r => r.Team).ToList();

        // Fetch challenges
        var challenges = await db.PyramidChallenges
            .Include(c => c.ChallengerTeam)
                .ThenInclude(t => t.TeamPlayers)
                    .ThenInclude(tp => tp.Player)
            .Include(c => c.DefenderTeam)
                .ThenInclude(t => t.TeamPlayers)
                    .ThenInclude(tp => tp.Player)
            .Where(c => c.CompetitionId == SelectedCompetition.Id)
            .OrderByDescending(c => c.ChallengeDate)
            .ToListAsync();

        ActiveChallenges = challenges.Where(c => c.Status == 0).ToList();
        PastChallenges = challenges.Where(c => c.Status != 0).Take(10).ToList();

        var inPoolPlayerIds = TournamentRegistrations.Select(r => r.PlayerId).ToHashSet();
        var inRankPlayerIds = ranks.SelectMany(r => r.Team.TeamPlayers.Select(tp => tp.PlayerId)).ToHashSet();
        UnregisteredPlayers = AllPlayers
            .Where(p => !inPoolPlayerIds.Contains(p.Id) && !inRankPlayerIds.Contains(p.Id))
            .OrderBy(p => p.Lastname)
            .ThenBy(p => p.Firstname)
            .ToList();

        // Find Current Player's Team & Rank in this competition
        if (CurrentPlayer != null)
        {
            MyRank = ranks.FirstOrDefault(r => r.Team.TeamPlayers.Any(tp => tp.PlayerId == CurrentPlayer.Id));
            MyTeam = MyRank?.Team;

            // List available double partners (players not yet registered in this pyramid and not current user)
            AvailablePartners = AllPlayers.Where(p => p.Id != CurrentPlayer.Id && !inRankPlayerIds.Contains(p.Id)).ToList();
        }

        // Build Pyramid Levels (Level 1 has 1 rank, Level 2 has 2 ranks, Level 3 has 3 ranks, etc.)
        int rankIndex = 0;
        int levelNum = 1;

        while (rankIndex < ranks.Count)
        {
            var level = new PyramidLevel { LevelNumber = levelNum };
            int levelSize = levelNum;

            for (int i = 0; i < levelSize && rankIndex < ranks.Count; i++)
            {
                var currentRank = ranks[rankIndex];
                var activeChallenge = ActiveChallenges.FirstOrDefault(c => 
                    c.ChallengerTeamId == currentRank.TeamId || c.DefenderTeamId == currentRank.TeamId);

                bool isMyTeam = MyTeam != null && currentRank.TeamId == MyTeam.Id;

                // Challenge Rule: My team can challenge someone up to 3 ranks above, provided neither team is in an active challenge
                bool canBeChallenged = false;
                if (MyRank != null && !isMyTeam && MyRank.Rank > currentRank.Rank)
                {
                    bool IAmInChallenge = ActiveChallenges.Any(c => c.ChallengerTeamId == MyTeam!.Id || c.DefenderTeamId == MyTeam!.Id);
                    bool TargetIsInChallenge = activeChallenge != null;
                    int rankDifference = MyRank.Rank - currentRank.Rank;

                    if (!IAmInChallenge && !TargetIsInChallenge && rankDifference <= 3)
                    {
                        canBeChallenged = true;
                    }
                }

                level.Nodes.Add(new PyramidPositionNode
                {
                    PyramidRank = currentRank,
                    ActiveChallenge = activeChallenge,
                    IsMyTeam = isMyTeam,
                    CanBeChallengedByCurrentUser = canBeChallenged
                });

                rankIndex++;
            }

            PyramidLevels.Add(level);
            levelNum++;
        }

        return Page();
    }

    public IActionResult OnPostSelectCompetition(int competitionId)
    {
        HttpContext.Session.SetInt32("SelectedPyramidCompId", competitionId);
        return RedirectToPage(new { competitionId });
    }

    // USER SELF-JOIN ACTION
    public async Task<IActionResult> OnPostJoinPyramidAsync(int competitionId, int? partnerId)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        if (CurrentPlayer == null)
        {
            return RedirectToPage(new { competitionId, message = "Bitte melde dich zuerst an.", isError = true });
        }

        var comp = await db.Competitions.FirstOrDefaultAsync(c => c.Id == competitionId);
        if (comp == null)
        {
            return RedirectToPage(new { message = "Wettbewerb nicht gefunden.", isError = true });
        }

        // Check if user is already registered in this pyramid
        var existingTeam = await db.TeamPlayer
            .Include(tp => tp.Team)
            .Where(tp => tp.Team.CompetitionId == competitionId && tp.PlayerId == CurrentPlayer.Id)
            .FirstOrDefaultAsync();

        if (existingTeam != null)
        {
            return RedirectToPage(new { competitionId, message = "Du nimmst bereits an dieser Pyramide teil.", isError = true });
        }

        Player? p2 = null;
        if (!comp.IsSingle)
        {
            if (!partnerId.HasValue || partnerId.Value == CurrentPlayer.Id)
            {
                return RedirectToPage(new { competitionId, message = "Bitte wähle deinen Doppelpartner aus.", isError = true });
            }

            p2 = await db.Players.FirstOrDefaultAsync(p => p.Id == partnerId.Value);
            if (p2 == null)
            {
                return RedirectToPage(new { competitionId, message = "Doppelpartner nicht gefunden.", isError = true });
            }

            var existingPartnerTeam = await db.TeamPlayer
                .Include(tp => tp.Team)
                .Where(tp => tp.Team.CompetitionId == competitionId && tp.PlayerId == p2.Id)
                .FirstOrDefaultAsync();

            if (existingPartnerTeam != null)
            {
                return RedirectToPage(new { competitionId, message = $"{p2} nimmt bereits an dieser Pyramide teil.", isError = true });
            }
        }

        // Create new Team
        var newTeam = new Team
        {
            CompetitionId = competitionId,
            BracketNo = 0
        };
        db.Teams.Add(newTeam);
        await db.SaveChangesAsync();

        // Add TeamPlayers
        db.TeamPlayer.Add(new TeamPlayer { TeamId = newTeam.Id, PlayerId = CurrentPlayer.Id });
        if (p2 != null)
        {
            db.TeamPlayer.Add(new TeamPlayer { TeamId = newTeam.Id, PlayerId = p2.Id });
        }
        await db.SaveChangesAsync();

        // Assign Rank at the bottom of the pyramid
        int maxRank = await db.PyramidRanks
            .Where(r => r.CompetitionId == competitionId)
            .Select(r => (int?)r.Rank)
            .MaxAsync() ?? 0;

        var pyramidRank = new PyramidRank
        {
            CompetitionId = competitionId,
            TeamId = newTeam.Id,
            Rank = maxRank + 1
        };
        db.PyramidRanks.Add(pyramidRank);
        await db.SaveChangesAsync();

        return RedirectToPage(new { competitionId, message = "Du wurdest erfolgreich in die Pyramide eingetragen! Viel Erfolg!" });
    }

    public async Task<IActionResult> OnPostCreateCompetitionAsync(string competitionName, bool isSingle, DateTime? registrationUntil)
    {
        if (!User.IsInRole("Admin"))
        {
            return RedirectToPage(new { message = "Zugriff verweigert.", isError = true });
        }

        if (string.IsNullOrWhiteSpace(competitionName))
        {
            return RedirectToPage(new { message = "Bitte einen Namen für die Pyramide eingeben.", isError = true });
        }

        var newComp = new Competition
        {
            Name = competitionName.Trim(),
            IsSingle = isSingle,
            IsPyramid = true,
            RegistrationUntil = registrationUntil ?? DateTime.Now.AddDays(14)
        };

        db.Competitions.Add(newComp);
        await db.SaveChangesAsync();

        HttpContext.Session.SetInt32("SelectedPyramidCompId", newComp.Id);
        return RedirectToPage(new { competitionId = newComp.Id, message = $"Pyramide '{newComp.Name}' wurde erfolgreich erstellt!" });
    }

    public async Task<IActionResult> OnPostDeleteCompetitionAsync(int competitionId)
    {
        if (!User.IsInRole("Admin"))
        {
            return RedirectToPage(new { competitionId, message = "Zugriff verweigert.", isError = true });
        }

        var challenges = await db.PyramidChallenges.Where(c => c.CompetitionId == competitionId).ToListAsync();
        db.PyramidChallenges.RemoveRange(challenges);

        var ranks = await db.PyramidRanks.Where(r => r.CompetitionId == competitionId).ToListAsync();
        db.PyramidRanks.RemoveRange(ranks);

        var teamPlayers = await db.TeamPlayer.Where(tp => tp.Team.CompetitionId == competitionId).ToListAsync();
        db.TeamPlayer.RemoveRange(teamPlayers);

        var teams = await db.Teams.Where(t => t.CompetitionId == competitionId).ToListAsync();
        db.Teams.RemoveRange(teams);

        var comp = await db.Competitions.FirstOrDefaultAsync(c => c.Id == competitionId);
        if (comp != null)
        {
            db.Competitions.Remove(comp);
        }

        await db.SaveChangesAsync();

        HttpContext.Session.Remove("SelectedPyramidCompId");
        return RedirectToPage(new { message = "Pyramide wurde erfolgreich gelöscht." });
    }

    public async Task<IActionResult> OnPostAddTeamAsync(int competitionId, int player1Id, int? player2Id)
    {
        if (!User.IsInRole("Admin"))
        {
            return RedirectToPage(new { competitionId, message = "Zugriff verweigert.", isError = true });
        }

        var comp = await db.Competitions.FirstOrDefaultAsync(c => c.Id == competitionId);
        if (comp == null)
        {
            return RedirectToPage(new { message = "Wettbewerb nicht gefunden.", isError = true });
        }

        // Check player 1
        var p1 = await db.Players.FirstOrDefaultAsync(p => p.Id == player1Id);
        if (p1 == null)
        {
            return RedirectToPage(new { competitionId, message = "Spieler 1 nicht gefunden.", isError = true });
        }

        var existingTeamWithP1 = await db.TeamPlayer
            .Include(tp => tp.Team)
            .Where(tp => tp.Team.CompetitionId == competitionId && tp.PlayerId == player1Id)
            .FirstOrDefaultAsync();

        if (existingTeamWithP1 != null)
        {
            return RedirectToPage(new { competitionId, message = $"{p1} ist bereits in dieser Pyramide eingetragen.", isError = true });
        }

        Player? p2 = null;
        if (!comp.IsSingle)
        {
            if (!player2Id.HasValue || player2Id.Value == player1Id)
            {
                return RedirectToPage(new { competitionId, message = "Für eine Doppel-Pyramide müssen zwei verschiedene Spieler gewählt werden.", isError = true });
            }

            p2 = await db.Players.FirstOrDefaultAsync(p => p.Id == player2Id.Value);
            if (p2 == null)
            {
                return RedirectToPage(new { competitionId, message = "Spieler 2 nicht gefunden.", isError = true });
            }

            var existingTeamWithP2 = await db.TeamPlayer
                .Include(tp => tp.Team)
                .Where(tp => tp.Team.CompetitionId == competitionId && tp.PlayerId == p2.Id)
                .FirstOrDefaultAsync();

            if (existingTeamWithP2 != null)
            {
                return RedirectToPage(new { competitionId, message = $"{p2} ist bereits in dieser Pyramide eingetragen.", isError = true });
            }
        }

        // Create new Team
        var newTeam = new Team
        {
            CompetitionId = competitionId,
            BracketNo = 0
        };
        db.Teams.Add(newTeam);
        await db.SaveChangesAsync();

        // Add TeamPlayers
        db.TeamPlayer.Add(new TeamPlayer { TeamId = newTeam.Id, PlayerId = p1.Id });
        if (p2 != null)
        {
            db.TeamPlayer.Add(new TeamPlayer { TeamId = newTeam.Id, PlayerId = p2.Id });
        }
        await db.SaveChangesAsync();

        // Next Rank Number
        int maxRank = await db.PyramidRanks
            .Where(r => r.CompetitionId == competitionId)
            .Select(r => (int?)r.Rank)
            .MaxAsync() ?? 0;

        var pyramidRank = new PyramidRank
        {
            CompetitionId = competitionId,
            TeamId = newTeam.Id,
            Rank = maxRank + 1
        };
        db.PyramidRanks.Add(pyramidRank);
        await db.SaveChangesAsync();

        return RedirectToPage(new { competitionId, message = "Teilnehmer erfolgreich zur Pyramide hinzugefügt!" });
    }

    public async Task<IActionResult> OnPostChallengeAsync(int competitionId, int defenderTeamId)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        if (CurrentPlayer == null)
        {
            return RedirectToPage(new { competitionId, message = "Bitte melde dich an.", isError = true });
        }

        var comp = await db.Competitions.FirstOrDefaultAsync(c => c.Id == competitionId);

        var myRank = await db.PyramidRanks
            .Include(r => r.Team)
                .ThenInclude(t => t.TeamPlayers)
                    .ThenInclude(tp => tp.Player)
            .FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.Team.TeamPlayers.Any(tp => tp.PlayerId == CurrentPlayer.Id));

        if (myRank == null)
        {
            return RedirectToPage(new { competitionId, message = "Du nimmst nicht an dieser Pyramide teil.", isError = true });
        }

        var defenderRank = await db.PyramidRanks
            .Include(r => r.Team)
                .ThenInclude(t => t.TeamPlayers)
                    .ThenInclude(tp => tp.Player)
                        .ThenInclude(p => p.IdentityUser)
            .Include(r => r.Team)
                .ThenInclude(t => t.TeamPlayers)
                    .ThenInclude(tp => tp.Player)
                        .ThenInclude(p => p.NotificationSettings)
            .FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.TeamId == defenderTeamId);

        if (defenderRank == null)
        {
            return RedirectToPage(new { competitionId, message = "Gefordertes Team nicht gefunden.", isError = true });
        }

        if (myRank.Rank <= defenderRank.Rank)
        {
            return RedirectToPage(new { competitionId, message = "Du kannst nur Teams herausfordern, die im Rang über dir stehen.", isError = true });
        }

        if (myRank.Rank - defenderRank.Rank > 3)
        {
            return RedirectToPage(new { competitionId, message = "Du kannst nur Teams bis zu 3 Ränge über dir herausfordern.", isError = true });
        }

        // Check existing open challenge for either team
        bool activeChallengeExists = await db.PyramidChallenges.AnyAsync(c =>
            c.CompetitionId == competitionId && c.Status == 0 &&
            (c.ChallengerTeamId == myRank.TeamId || c.DefenderTeamId == myRank.TeamId ||
             c.ChallengerTeamId == defenderTeamId || c.DefenderTeamId == defenderTeamId));

        if (activeChallengeExists)
        {
            return RedirectToPage(new { competitionId, message = "Mindestens eines der Teams befindet sich bereits in einer aktiven Forderung.", isError = true });
        }

        var challenge = new PyramidChallenge
        {
            CompetitionId = competitionId,
            ChallengerTeamId = myRank.TeamId,
            DefenderTeamId = defenderTeamId,
            ChallengeDate = DateTime.UtcNow,
            Status = 0
        };

        db.PyramidChallenges.Add(challenge);
        await db.SaveChangesAsync();

        // Send email notification if enabled in defender's notification settings
        if (defenderRank.Team != null && comp != null)
        {
            var challengerNames = string.Join(" & ", myRank.Team.TeamPlayers.Select(tp => $"{tp.Player.Firstname} {tp.Player.Lastname}"));
            var compName = comp.Name;

            foreach (var tp in defenderRank.Team.TeamPlayers)
            {
                var defenderPlayer = tp.Player;
                if (defenderPlayer?.IdentityUser?.Email != null &&
                    (defenderPlayer.NotificationSettings == null || defenderPlayer.NotificationSettings.EmailOnPyramidChallenge))
                {
                    var subject = $"🎾 Neue Forderung in der Pyramide '{compName}'!";
                    var body = $"Hallo {defenderPlayer.Firstname},<br><br>" +
                               $"Du wurdest in der Pyramide <strong>{compName}</strong> von <strong>{challengerNames}</strong> herausgefordert!<br><br>" +
                               $"Bitte vereinbart zeitnah einen Spieltermin und tragt das Ergebnis nach dem Match in der Anwendung ein.<br><br>" +
                               $"Viel Erfolg!<br>Dein TennisBruck-Team";

                    _ = emailSender.SendEmailAsync(defenderPlayer.IdentityUser.Email, subject, body);
                }
            }
        }

        return RedirectToPage(new { competitionId, message = "Forderung erfolgreich ausgesprochen!" });
    }

    public async Task<IActionResult> OnPostSubmitResultAsync(int competitionId, int challengeId, int winnerTeamId, string score)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        if (CurrentPlayer == null)
        {
            return RedirectToPage(new { competitionId, message = "Bitte melde dich an.", isError = true });
        }

        var challenge = await db.PyramidChallenges
            .Include(c => c.ChallengerTeam)
            .Include(c => c.DefenderTeam)
            .FirstOrDefaultAsync(c => c.Id == challengeId);

        if (challenge == null || challenge.Status != 0)
        {
            return RedirectToPage(new { competitionId, message = "Forderung nicht gefunden oder bereits abgeschlossen.", isError = true });
        }

        bool isChallengerMember = await db.TeamPlayer.AnyAsync(tp => tp.TeamId == challenge.ChallengerTeamId && tp.PlayerId == CurrentPlayer.Id);
        bool isDefenderMember = await db.TeamPlayer.AnyAsync(tp => tp.TeamId == challenge.DefenderTeamId && tp.PlayerId == CurrentPlayer.Id);

        if (!User.IsInRole("Admin") && !isChallengerMember && !isDefenderMember)
        {
            return RedirectToPage(new { competitionId, message = "Zugriff verweigert.", isError = true });
        }

        challenge.Status = 1; // Completed
        challenge.WinnerTeamId = winnerTeamId;
        challenge.MatchDate = DateTime.UtcNow;
        challenge.Score = string.IsNullOrWhiteSpace(score) ? null : score.Trim();

        // SWAP RANK LOGIC: If Challenger wins, Challenger and Defender swap pyramid ranks!
        if (winnerTeamId == challenge.ChallengerTeamId)
        {
            var challengerRank = await db.PyramidRanks.FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.TeamId == challenge.ChallengerTeamId);
            var defenderRank = await db.PyramidRanks.FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.TeamId == challenge.DefenderTeamId);

            if (challengerRank != null && defenderRank != null)
            {
                int tempRank = challengerRank.Rank;
                challengerRank.Rank = defenderRank.Rank;
                defenderRank.Rank = tempRank;
            }
        }

        await db.SaveChangesAsync();

        string msg = winnerTeamId == challenge.ChallengerTeamId
            ? "Glückwunsch! Der Forderer hat gewonnen und übernimmt die höhere Pyramidenposition!"
            : "Das geforderte Team hat gewonnen und verteidigt seinen Rang!";

        return RedirectToPage(new { competitionId, message = msg });
    }

    public async Task<IActionResult> OnPostCancelChallengeAsync(int competitionId, int challengeId)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        if (CurrentPlayer == null)
        {
            return RedirectToPage(new { competitionId, message = "Bitte melde dich an.", isError = true });
        }

        var challenge = await db.PyramidChallenges.FirstOrDefaultAsync(c => c.Id == challengeId);
        if (challenge == null)
        {
            return RedirectToPage(new { competitionId, message = "Forderung nicht gefunden.", isError = true });
        }

        bool isChallengerMember = await db.TeamPlayer.AnyAsync(tp => tp.TeamId == challenge.ChallengerTeamId && tp.PlayerId == CurrentPlayer.Id);

        if (!User.IsInRole("Admin") && !isChallengerMember)
        {
            return RedirectToPage(new { competitionId, message = "Zugriff verweigert.", isError = true });
        }

        challenge.Status = 2; // Cancelled
        await db.SaveChangesAsync();

        return RedirectToPage(new { competitionId, message = "Forderung wurde storniert." });
    }

    public async Task<IActionResult> OnPostDeletePyramidRankAsync(int competitionId, int teamId)
    {
        if (!User.IsInRole("Admin"))
        {
            return RedirectToPage(new { competitionId, message = "Zugriff verweigert.", isError = true });
        }

        var rank = await db.PyramidRanks.FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.TeamId == teamId);
        if (rank != null)
        {
            db.PyramidRanks.Remove(rank);

            // Re-order remaining ranks sequentially
            var remainingRanks = await db.PyramidRanks
                .Where(r => r.CompetitionId == competitionId && r.Id != rank.Id)
                .OrderBy(r => r.Rank)
                .ToListAsync();

            int index = 1;
            foreach (var r in remainingRanks)
            {
                r.Rank = index++;
            }
        }

        var team = await db.Teams.Include(t => t.TeamPlayers).FirstOrDefaultAsync(t => t.Id == teamId);
        if (team != null)
        {
            db.TeamPlayer.RemoveRange(team.TeamPlayers);
            db.Teams.Remove(team);
        }

        await db.SaveChangesAsync();
        return RedirectToPage(new { competitionId, message = "Teilnehmer aus der Pyramide entfernt." });
    }

    public async Task<IActionResult> OnPostSaveMatchAsync(int? challengeId, int? matchId, string score)
    {
        int targetId = challengeId ?? matchId ?? 0;
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        if (CurrentPlayer == null)
        {
            return RedirectToPage(new { message = "Bitte melde dich an.", isError = true });
        }

        var challenge = await db.PyramidChallenges
            .Include(c => c.ChallengerTeam)
            .Include(c => c.DefenderTeam)
            .FirstOrDefaultAsync(c => c.Id == targetId);

        if (challenge == null || challenge.Status != 0)
        {
            return RedirectToPage(new { message = "Forderung nicht gefunden oder bereits abgeschlossen.", isError = true });
        }

        bool isChallengerMember = await db.TeamPlayer.AnyAsync(tp => tp.TeamId == challenge.ChallengerTeamId && tp.PlayerId == CurrentPlayer.Id);
        bool isDefenderMember = await db.TeamPlayer.AnyAsync(tp => tp.TeamId == challenge.DefenderTeamId && tp.PlayerId == CurrentPlayer.Id);

        if (!User.IsInRole("Admin") && !isChallengerMember && !isDefenderMember)
        {
            return RedirectToPage(new { message = "Zugriff verweigert.", isError = true });
        }

        if (string.IsNullOrWhiteSpace(score))
        {
            return RedirectToPage(new { competitionId = challenge.CompetitionId, message = "Bitte ein Ergebnis eingeben.", isError = true });
        }

        int setsWonChallenger = 0;
        int setsWonDefender = 0;

        try
        {
            var sets = score.Trim().Split(" ");
            foreach (var set in sets)
            {
                var games = set.Split(":");
                int g1 = int.Parse(games[0]);
                int g2 = int.Parse(games[1]);
                if (g1 > g2) setsWonChallenger++;
                else if (g2 > g1) setsWonDefender++;
            }
        }
        catch
        {
            return RedirectToPage(new { competitionId = challenge.CompetitionId, message = "Ungültiges Ergebnisformat (z.B. 6:4 6:2).", isError = true });
        }

        if (setsWonChallenger == setsWonDefender)
        {
            return RedirectToPage(new { competitionId = challenge.CompetitionId, message = "Unentschieden ist nicht erlaubt.", isError = true });
        }

        int winnerTeamId = setsWonChallenger > setsWonDefender ? challenge.ChallengerTeamId : challenge.DefenderTeamId;

        return await OnPostSubmitResultAsync(challenge.CompetitionId, targetId, winnerTeamId, score);
    }

    public async Task<IActionResult> OnPostAdminWalkoverAsync(int? challengeId, int? matchId, int walkoverTeamId)
    {
        int targetId = challengeId ?? matchId ?? 0;
        if (!User.IsInRole("Admin"))
        {
            return RedirectToPage(new { message = "Zugriff verweigert.", isError = true });
        }

        var challenge = await db.PyramidChallenges.FirstOrDefaultAsync(c => c.Id == targetId);
        if (challenge == null || challenge.Status != 0)
        {
            return RedirectToPage(new { message = "Forderung nicht gefunden.", isError = true });
        }

        int winnerTeamId = walkoverTeamId == challenge.ChallengerTeamId ? challenge.DefenderTeamId : challenge.ChallengerTeamId;
        return await OnPostSubmitResultAsync(challenge.CompetitionId, targetId, winnerTeamId, "w.o.");
    }

    public async Task<IActionResult> OnPostGiveWalkoverAsync(int? challengeId, int? matchId)
    {
        int targetId = challengeId ?? matchId ?? 0;
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        if (CurrentPlayer == null)
        {
            return RedirectToPage(new { message = "Bitte melde dich an.", isError = true });
        }

        var challenge = await db.PyramidChallenges.FirstOrDefaultAsync(c => c.Id == targetId);
        if (challenge == null || challenge.Status != 0)
        {
            return RedirectToPage(new { message = "Forderung nicht gefunden.", isError = true });
        }

        bool isChallenger = await db.TeamPlayer.AnyAsync(tp => tp.TeamId == challenge.ChallengerTeamId && tp.PlayerId == CurrentPlayer.Id);
        bool isDefender = await db.TeamPlayer.AnyAsync(tp => tp.TeamId == challenge.DefenderTeamId && tp.PlayerId == CurrentPlayer.Id);

        if (!isChallenger && !isDefender)
        {
            return RedirectToPage(new { message = "Zugriff verweigert.", isError = true });
        }

        int winnerTeamId = isChallenger ? challenge.DefenderTeamId : challenge.ChallengerTeamId;
        return await OnPostSubmitResultAsync(challenge.CompetitionId, targetId, winnerTeamId, "w.o.");
    }

    public async Task<IActionResult> OnPostRegisterSelfAsync(int competitionId)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        if (CurrentPlayer == null)
        {
            return RedirectToPage(new { competitionId, message = "Bitte melde dich an.", isError = true });
        }

        var comp = await db.Competitions.FirstOrDefaultAsync(c => c.Id == competitionId);
        if (comp == null)
        {
            return RedirectToPage(new { message = "Wettbewerb nicht gefunden.", isError = true });
        }

        if (DateTime.Now > comp.RegistrationUntil)
        {
            return RedirectToPage(new { competitionId, message = "Die Anmeldefrist für diese Pyramide ist abgelaufen.", isError = true });
        }

        var existingReg = await db.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.PlayerId == CurrentPlayer.Id);

        if (existingReg != null)
        {
            if (existingReg.HasWithdrawn)
            {
                existingReg.HasWithdrawn = false;
                existingReg.RegisteredAt = DateTime.UtcNow;
            }
            else
            {
                return RedirectToPage(new { competitionId, message = "Du bist bereits für diese Pyramide angemeldet.", isError = true });
            }
        }
        else
        {
            db.TournamentRegistrations.Add(new TournamentRegistration
            {
                CompetitionId = competitionId,
                PlayerId = CurrentPlayer.Id,
                RegisteredAt = DateTime.UtcNow,
                HasWithdrawn = false
            });
        }

        await db.SaveChangesAsync();
        return RedirectToPage(new { competitionId, message = "Du hast dich erfolgreich für das Doppel angemeldet!" });
    }

    public async Task<IActionResult> OnPostWithdrawSelfRegistrationAsync(int competitionId)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
        if (CurrentPlayer == null)
        {
            return RedirectToPage(new { competitionId, message = "Bitte melde dich an.", isError = true });
        }

        var reg = await db.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.PlayerId == CurrentPlayer.Id && !r.HasWithdrawn);

        if (reg != null)
        {
            reg.HasWithdrawn = true;
            await db.SaveChangesAsync();
        }

        return RedirectToPage(new { competitionId, message = "Deine Anmeldung wurde storniert." });
    }

    public async Task<IActionResult> OnPostSaveNewDateAsync(int competitionId, string newDate, string newTime)
    {
        if (!User.IsInRole("Admin"))
        {
            return RedirectToPage(new { competitionId, message = "Zugriff verweigert.", isError = true });
        }

        var comp = await db.Competitions.FirstOrDefaultAsync(c => c.Id == competitionId);
        if (comp == null)
        {
            return RedirectToPage(new { message = "Wettbewerb nicht gefunden.", isError = true });
        }

        try
        {
            var date = DateOnly.Parse(newDate);
            var time = TimeOnly.Parse(newTime);
            comp.RegistrationUntil = date.ToDateTime(time);
            await db.SaveChangesAsync();
            return RedirectToPage(new { competitionId, message = "Anmeldefrist erfolgreich aktualisiert!" });
        }
        catch
        {
            return RedirectToPage(new { competitionId, message = "Ungültiges Datumsformat.", isError = true });
        }
    }

    public async Task<IActionResult> OnPostGeneratePairsAsync(int? competitionId, int? ChampionshipId)
    {
        int targetCompId = competitionId ?? ChampionshipId ?? SelectedCompetition?.Id ?? 0;
        if (!User.IsInRole("Admin")) return Forbid();

        var comp = await db.Competitions.FirstOrDefaultAsync(c => c.Id == targetCompId);
        if (comp == null) return RedirectToPage(new { message = "Bewerb nicht gefunden.", isError = true });

        // 1. Gather all players currently in pool OR currently in existing teams for this competition
        var poolPlayers = await db.TournamentRegistrations
            .Include(tr => tr.Player)
            .Where(tr => tr.CompetitionId == targetCompId && !tr.HasWithdrawn)
            .Select(tr => tr.Player)
            .ToListAsync();

        var existingTeamPlayers = await db.Teams
            .Where(t => t.CompetitionId == targetCompId)
            .SelectMany(t => t.TeamPlayers.Select(tp => tp.Player))
            .ToListAsync();

        var registeredPlayers = poolPlayers.Concat(existingTeamPlayers)
            .Where(p => p != null)
            .DistinctBy(p => p.Id)
            .ToList();

        if (registeredPlayers.Count < 2)
        {
            return RedirectToPage(new { competitionId = targetCompId, message = "Es werden mindestens 2 angemeldete Spieler benötigt, um Paare zu generieren.", isError = true });
        }

        // 2. Remove existing ranks and teams safely
        var existingRanks = await db.PyramidRanks
            .Where(r => r.CompetitionId == targetCompId)
            .ToListAsync();
        db.PyramidRanks.RemoveRange(existingRanks);

        var existingTeams = await db.Teams
            .Include(t => t.TeamPlayers)
            .Where(t => t.CompetitionId == targetCompId)
            .ToListAsync();

        foreach (var t in existingTeams)
        {
            db.TeamPlayer.RemoveRange(t.TeamPlayers);
        }
        db.Teams.RemoveRange(existingTeams);
        await db.SaveChangesAsync();

        var rng = new Random();
        var shuffled = registeredPlayers.OrderBy(_ => rng.Next()).ToList();

        int rankCounter = 1;
        for (int i = 0; i < shuffled.Count - 1; i += 2)
        {
            var p1 = shuffled[i];
            var p2 = shuffled[i + 1];

            var newTeam = new Team
            {
                CompetitionId = targetCompId,
                BracketNo = 0,
                TeamPlayers = new List<TeamPlayer>
                {
                    new() { PlayerId = p1.Id },
                    new() { PlayerId = p2.Id }
                }
            };
            db.Teams.Add(newTeam);
            await db.SaveChangesAsync();

            db.PyramidRanks.Add(new PyramidRank
            {
                CompetitionId = targetCompId,
                TeamId = newTeam.Id,
                Rank = rankCounter++
            });
        }

        await db.SaveChangesAsync();
        string note = shuffled.Count % 2 != 0 ? " (1 Spieler blieb übrig)" : "";
        return RedirectToPage(new { competitionId = targetCompId, message = $"Doppel-Paare wurden zufällig ausgelost und eingereiht!{note}" });
    }

    public async Task<IActionResult> OnPostSavePairsAsync(List<PlayerCompetitionPairs> pairs, int? competitionId, int? ChampionshipId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        int targetCompId = competitionId ?? ChampionshipId ?? SelectedCompetition?.Id ?? 0;

        var assignedPlayerIds = new HashSet<int>();

        foreach (var pair in pairs)
        {
            if (!pair.SinglePlayerId.HasValue && !pair.DoublePlayerId.HasValue) continue;

            if (pair.SinglePlayerId.HasValue && pair.DoublePlayerId.HasValue && pair.SinglePlayerId.Value == pair.DoublePlayerId.Value)
            {
                var player = await db.Players.FindAsync(pair.SinglePlayerId.Value);
                string name = player != null ? $"{player.Firstname} {player.Lastname}" : "Ein Spieler";
                return RedirectToPage(new { competitionId = targetCompId, message = $"Fehler: {name} kann nicht mit sich selbst ein Paar bilden.", isError = true });
            }

            if (pair.SinglePlayerId.HasValue)
            {
                if (assignedPlayerIds.Contains(pair.SinglePlayerId.Value))
                {
                    var player = await db.Players.FindAsync(pair.SinglePlayerId.Value);
                    string name = player != null ? $"{player.Firstname} {player.Lastname}" : "Ein Spieler";
                    return RedirectToPage(new { competitionId = targetCompId, message = $"Fehler: {name} ist doppelt zugewiesen.", isError = true });
                }
                assignedPlayerIds.Add(pair.SinglePlayerId.Value);
            }

            if (pair.DoublePlayerId.HasValue)
            {
                if (assignedPlayerIds.Contains(pair.DoublePlayerId.Value))
                {
                    var player = await db.Players.FindAsync(pair.DoublePlayerId.Value);
                    string name = player != null ? $"{player.Firstname} {player.Lastname}" : "Ein Spieler";
                    return RedirectToPage(new { competitionId = targetCompId, message = $"Fehler: {name} ist doppelt zugewiesen.", isError = true });
                }
                assignedPlayerIds.Add(pair.DoublePlayerId.Value);
            }
        }

        int maxRank = await db.PyramidRanks
            .Where(r => r.CompetitionId == targetCompId)
            .Select(r => (int?)r.Rank)
            .MaxAsync() ?? 0;

        foreach (var pair in pairs)
        {
            if (!pair.SinglePlayerId.HasValue || !pair.DoublePlayerId.HasValue) continue;

            int p1 = pair.SinglePlayerId.Value;
            int p2 = pair.DoublePlayerId.Value;

            if (pair.TeamId.HasValue && pair.TeamId.Value > 0)
            {
                var team = await db.Teams
                    .Include(t => t.TeamPlayers)
                    .FirstOrDefaultAsync(t => t.Id == pair.TeamId.Value && t.CompetitionId == targetCompId);

                if (team != null)
                {
                    db.TeamPlayer.RemoveRange(team.TeamPlayers);
                    team.TeamPlayers = new List<TeamPlayer>
                    {
                        new() { PlayerId = p1 },
                        new() { PlayerId = p2 }
                    };

                    var rank = await db.PyramidRanks.FirstOrDefaultAsync(r => r.TeamId == team.Id && r.CompetitionId == targetCompId);
                    if (rank == null)
                    {
                        maxRank++;
                        db.PyramidRanks.Add(new PyramidRank
                        {
                            CompetitionId = targetCompId,
                            TeamId = team.Id,
                            Rank = maxRank
                        });
                    }
                }
            }
            else
            {
                var newTeam = new Team
                {
                    CompetitionId = targetCompId,
                    BracketNo = 0,
                    TeamPlayers = new List<TeamPlayer>
                    {
                        new() { PlayerId = p1 },
                        new() { PlayerId = p2 }
                    }
                };
                db.Teams.Add(newTeam);
                await db.SaveChangesAsync();

                maxRank++;
                db.PyramidRanks.Add(new PyramidRank
                {
                    CompetitionId = targetCompId,
                    TeamId = newTeam.Id,
                    Rank = maxRank
                });
            }
        }

        await db.SaveChangesAsync();
        return RedirectToPage(new { competitionId = targetCompId, message = "Doppel-Paare wurden gespeichert und in der Pyramide eingereiht!" });
    }

    public async Task<IActionResult> OnPostDeleteTeamAsync(int teamId)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        var team = await db.Teams
            .Include(t => t.TeamPlayers)
            .FirstOrDefaultAsync(t => t.Id == teamId);

        if (team == null) return RedirectToPage(new { message = "Team nicht gefunden.", isError = true });

        int compId = team.CompetitionId;

        var rank = await db.PyramidRanks.FirstOrDefaultAsync(r => r.TeamId == teamId);
        if (rank != null)
        {
            db.PyramidRanks.Remove(rank);
        }

        db.TeamPlayer.RemoveRange(team.TeamPlayers);
        db.Teams.Remove(team);
        await db.SaveChangesAsync();

        var remainingRanks = await db.PyramidRanks
            .Where(r => r.CompetitionId == compId)
            .OrderBy(r => r.Rank)
            .ToListAsync();

        for (int i = 0; i < remainingRanks.Count; i++)
        {
            remainingRanks[i].Rank = i + 1;
        }

        await db.SaveChangesAsync();
        return RedirectToPage(new { competitionId = compId, message = "Team wurde gelöscht." });
    }

    public async Task<IActionResult> OnPostAdminRegisterPlayerAsync(int competitionId, int playerId)
    {
        if (!User.IsInRole("Admin"))
        {
            return RedirectToPage(new { competitionId, message = "Zugriff verweigert.", isError = true });
        }

        var comp = await db.Competitions.FirstOrDefaultAsync(c => c.Id == competitionId);
        if (comp == null)
        {
            return RedirectToPage(new { message = "Wettbewerb nicht gefunden.", isError = true });
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId);
        if (player == null)
        {
            return RedirectToPage(new { competitionId, message = "Spieler nicht gefunden.", isError = true });
        }

        if (comp.IsSingle)
        {
            var existingRank = await db.PyramidRanks
                .Include(r => r.Team).ThenInclude(t => t.TeamPlayers)
                .FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.Team.TeamPlayers.Any(tp => tp.PlayerId == playerId));

            if (existingRank != null)
            {
                return RedirectToPage(new { competitionId, message = $"{player.Firstname} {player.Lastname} ist bereits in dieser Pyramide.", isError = true });
            }

            var newTeam = new Team
            {
                CompetitionId = competitionId,
                BracketNo = 0,
                TeamPlayers = new List<TeamPlayer> { new() { PlayerId = playerId } }
            };
            db.Teams.Add(newTeam);
            await db.SaveChangesAsync();

            int maxRank = await db.PyramidRanks
                .Where(r => r.CompetitionId == competitionId)
                .Select(r => (int?)r.Rank)
                .MaxAsync() ?? 0;

            db.PyramidRanks.Add(new PyramidRank
            {
                CompetitionId = competitionId,
                TeamId = newTeam.Id,
                Rank = maxRank + 1
            });
            await db.SaveChangesAsync();

            return RedirectToPage(new { competitionId, message = $"{player.Firstname} {player.Lastname} wurde der Einzel-Pyramide hinzugefügt!" });
        }
        else
        {
            var existingReg = await db.TournamentRegistrations
                .FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.PlayerId == playerId);

            if (existingReg != null)
            {
                if (existingReg.HasWithdrawn)
                {
                    existingReg.HasWithdrawn = false;
                    existingReg.RegisteredAt = DateTime.UtcNow;
                }
                else
                {
                    return RedirectToPage(new { competitionId, message = $"{player.Firstname} {player.Lastname} ist bereits angemeldet.", isError = true });
                }
            }
            else
            {
                db.TournamentRegistrations.Add(new TournamentRegistration
                {
                    CompetitionId = competitionId,
                    PlayerId = playerId,
                    RegisteredAt = DateTime.UtcNow,
                    HasWithdrawn = false
                });
            }

            await db.SaveChangesAsync();
            return RedirectToPage(new { competitionId, message = $"{player.Firstname} {player.Lastname} wurde dem Anmelde-Pool hinzugefügt!" });
        }
    }

    public async Task<IActionResult> OnPostAdminRemoveRegistrationAsync(int competitionId, int playerId)
    {
        if (!User.IsInRole("Admin"))
        {
            return RedirectToPage(new { competitionId, message = "Zugriff verweigert.", isError = true });
        }

        var reg = await db.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.PlayerId == playerId && !r.HasWithdrawn);

        if (reg != null)
        {
            reg.HasWithdrawn = true;
            await db.SaveChangesAsync();
        }

        return RedirectToPage(new { competitionId, message = "Spieler aus dem Pool entfernt." });
    }
}