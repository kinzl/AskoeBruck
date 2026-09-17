using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using TennisBruck.Features.Pyramid.Models;
using TennisDb;

namespace TennisBruck.Features.Pyramid;

public class PyramidService(TennisContext db, IEmailSender emailSender, WebPushNotificationService? pushService = null)
{
    public List<PyramidLevel> BuildPyramidLevels(
        List<PyramidRank> ranks,
        List<PyramidChallenge> activeChallenges,
        PyramidRank? myRank,
        Team? myTeam)
    {
        var pyramidLevels = new List<PyramidLevel>();
        int rankIndex = 0;
        int levelNum = 1;

        while (rankIndex < ranks.Count)
        {
            var level = new PyramidLevel { LevelNumber = levelNum };
            int levelSize = levelNum;

            for (int i = 0; i < levelSize && rankIndex < ranks.Count; i++)
            {
                var currentRank = ranks[rankIndex];
                var activeChallenge = activeChallenges.FirstOrDefault(c =>
                    c.ChallengerTeamId == currentRank.TeamId || c.DefenderTeamId == currentRank.TeamId);

                bool isMyTeam = myTeam != null && currentRank.TeamId == myTeam.Id;

                bool canBeChallenged = false;
                if (myRank != null && !isMyTeam && myRank.Rank > currentRank.Rank)
                {
                    bool iAmInChallenge = activeChallenges.Any(c =>
                        c.ChallengerTeamId == myTeam!.Id || c.DefenderTeamId == myTeam!.Id);
                    bool targetIsInChallenge = activeChallenge != null;
                    int rankDifference = myRank.Rank - currentRank.Rank;

                    if (!iAmInChallenge && !targetIsInChallenge && rankDifference <= 3)
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

            pyramidLevels.Add(level);
            levelNum++;
        }

        return pyramidLevels;
    }

    public async Task<(bool Success, string Message)> IssueChallengeAsync(
        int competitionId,
        int challengerPlayerId,
        int defenderTeamId)
    {
        var comp = await db.Competitions.FirstOrDefaultAsync(c => c.Id == competitionId);

        var myRank = await db.PyramidRanks
            .Include(r => r.Team)
            .ThenInclude(t => t.TeamPlayers)
            .ThenInclude(tp => tp.Player)
            .FirstOrDefaultAsync(r =>
                r.CompetitionId == competitionId && r.Team.TeamPlayers.Any(tp => tp.PlayerId == challengerPlayerId));

        if (myRank == null)
            return (false, "Du nimmst nicht an dieser Pyramide teil.");

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
            return (false, "Gefordertes Team nicht gefunden.");

        if (myRank.Rank <= defenderRank.Rank)
            return (false, "Du kannst nur Teams herausfordern, die im Rang über dir stehen.");

        if (myRank.Rank - defenderRank.Rank > 3)
            return (false, "Du kannst nur Teams bis zu 3 Ränge über dir herausfordern.");

        bool activeChallengeExists = await db.PyramidChallenges.AnyAsync(c =>
            c.CompetitionId == competitionId && c.Status == 0 &&
            (c.ChallengerTeamId == myRank.TeamId || c.DefenderTeamId == myRank.TeamId ||
             c.ChallengerTeamId == defenderTeamId || c.DefenderTeamId == defenderTeamId));

        if (activeChallengeExists)
            return (false, "Mindestens eines der Teams befindet sich bereits in einer aktiven Forderung.");

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

        if (comp != null)
        {
            var challengerNames = string.Join(" & ",
                myRank.Team.TeamPlayers.Select(tp => $"{tp.Player.Firstname} {tp.Player.Lastname}"));
            var compName = comp.Name;

            foreach (var tp in defenderRank.Team.TeamPlayers)
            {
                var defenderPlayer = tp.Player;
                if (defenderPlayer.IdentityUser?.Email != null &&
                    defenderPlayer.NotificationSettings?.EmailOnPyramidChallenge == true)
                {
                    var subject = $"🎾 Neue Forderung in der Pyramide '{compName}'!";
                    var body = $"Hallo {defenderPlayer.Firstname},<br><br>" +
                               $"Du wurdest in der Pyramide <strong>{compName}</strong> von <strong>{challengerNames}</strong> herausgefordert!<br><br>" +
                               $"Bitte vereinbart zeitnah einen Spieltermin und tragt das Ergebnis nach dem Match in der Anwendung ein.<br><br>" +
                               $"Viel Erfolg!<br>Dein TennisBruck-Team";

                    _ = emailSender.SendEmailAsync(defenderPlayer.IdentityUser.Email, subject, body);
                }

                if (pushService != null)
                {
                    _ = pushService.SendNotificationAsync(defenderPlayer.Id, $"🎾 Forderung in '{compName}'!", $"{challengerNames} hat dich herausgefordert.", "/Pyramid");
                }
            }
        }

        return (true, "Forderung erfolgreich ausgesprochen!");
    }

    public async Task<(bool Success, string Message)> SubmitResultAsync(
        int competitionId,
        int challengeId,
        int winnerTeamId,
        string? score,
        int currentUserId,
        bool isAdmin)
    {
        var challenge = await db.PyramidChallenges
            .Include(c => c.ChallengerTeam)
            .Include(c => c.DefenderTeam)
            .FirstOrDefaultAsync(c => c.Id == challengeId);

        if (challenge == null || challenge.Status != 0)
            return (false, "Forderung nicht gefunden oder bereits abgeschlossen.");

        bool isChallengerMember = await db.TeamPlayer.AnyAsync(tp =>
            tp.TeamId == challenge.ChallengerTeamId && tp.PlayerId == currentUserId);
        bool isDefenderMember = await db.TeamPlayer.AnyAsync(tp =>
            tp.TeamId == challenge.DefenderTeamId && tp.PlayerId == currentUserId);

        if (!isAdmin && !isChallengerMember && !isDefenderMember)
            return (false, "Zugriff verweigert.");

        challenge.Status = 1;
        challenge.WinnerTeamId = winnerTeamId;
        challenge.MatchDate = DateTime.UtcNow;
        challenge.Score = string.IsNullOrWhiteSpace(score) ? null : score.Trim();

        if (winnerTeamId == challenge.ChallengerTeamId)
        {
            var challengerRank = await db.PyramidRanks.FirstOrDefaultAsync(r =>
                r.CompetitionId == competitionId && r.TeamId == challenge.ChallengerTeamId);
            var defenderRank = await db.PyramidRanks.FirstOrDefaultAsync(r =>
                r.CompetitionId == competitionId && r.TeamId == challenge.DefenderTeamId);

            if (challengerRank != null && defenderRank != null)
            {
                (challengerRank.Rank, defenderRank.Rank) = (defenderRank.Rank, challengerRank.Rank);
            }
        }

        await db.SaveChangesAsync();

        string msg = winnerTeamId == challenge.ChallengerTeamId
            ? "Glückwunsch! Der Forderer hat gewonnen und übernimmt die höhere Pyramidenposition!"
            : "Das geforderte Team hat gewonnen und verteidigt seinen Rang!";

        return (true, msg);
    }

    public async Task<(bool Success, string Message)> CancelChallengeAsync(
        int challengeId,
        int currentUserId,
        bool isAdmin)
    {
        var challenge = await db.PyramidChallenges.FirstOrDefaultAsync(c => c.Id == challengeId);
        if (challenge == null)
            return (false, "Forderung nicht gefunden.");

        bool isChallengerMember = await db.TeamPlayer.AnyAsync(tp =>
            tp.TeamId == challenge.ChallengerTeamId && tp.PlayerId == currentUserId);

        if (!isAdmin && !isChallengerMember)
            return (false, "Zugriff verweigert.");

        challenge.Status = 2;
        await db.SaveChangesAsync();

        return (true, "Forderung wurde storniert.");
    }

    public async Task DeletePyramidRankAsync(int competitionId, int teamId)
    {
        var rank = await db.PyramidRanks.FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.TeamId == teamId);
        if (rank != null)
        {
            db.PyramidRanks.Remove(rank);

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
    }
}
