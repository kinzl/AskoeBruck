using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using TennisDb;
using Match = TennisDb.Match;

namespace TennisBruck.Features.Championship;

public class ChampionshipService(
    TennisContext db,
    TournamentBracketEngine bracketEngine,
    GroupStageEngine groupStageEngine,
    IEmailSender emailSender)
{
    public void DeleteCompetitionDependencies(int competitionId, bool deleteCompetitionSelf = false)
    {
        var groupIds = db.Groups.Where(g => g.CompetitionId == competitionId).Select(g => g.Id).ToList();
        var knockoutMatchIds = db.KnockoutMatch.Where(k => k.CompetitionId == competitionId).Select(k => k.Id).ToList();

        db.Sets.Where(s =>
                (s.Match.Group != null && groupIds.Contains(s.Match.Group.Id)) || knockoutMatchIds.Contains(s.Match.Id))
            .ExecuteDelete();
        db.KnockoutMatch.Where(k => k.CompetitionId == competitionId).ExecuteDelete();
        db.Matches.Where(m => m.Group != null && groupIds.Contains(m.Group.Id)).ExecuteDelete();
        db.GroupTeams.Where(gt => groupIds.Contains(gt.GroupId)).ExecuteDelete();
        db.Groups.Where(g => g.CompetitionId == competitionId).ExecuteDelete();
        db.TeamPlayer.Where(tp => tp.Team.CompetitionId == competitionId).ExecuteDelete();
        db.Teams.Where(t => t.CompetitionId == competitionId).ExecuteDelete();

        if (deleteCompetitionSelf)
        {
            db.TournamentRegistrations.Where(r => r.CompetitionId == competitionId).ExecuteDelete();
            db.Competitions.Where(c => c.Id == competitionId).ExecuteDelete();
        }
    }

    public async Task<(bool Success, string Message)> WithdrawPlayerAsync(int playerId, int competitionId)
    {
        var userTeams = await db.Teams
            .Include(t => t.TeamPlayers)
            .Where(t => t.CompetitionId == competitionId && t.TeamPlayers.Any(p => p.PlayerId == playerId))
            .ToListAsync();

        var teamIds = userTeams.Select(t => t.Id).ToList();

        if (!teamIds.Any()) return (false, "Spieler ist in keinen Teams dieses Bewerbs.");

        var unplayedMatches = await db.Matches
            .Include(x => x.Sets)
            .Include(x => x.Winner)
            .Include(x => x.Team1)
            .Include(x => x.Team2)
            .Where(m => (m.Team1 != null && teamIds.Contains(m.Team1.Id)) ||
                        (m.Team2 != null && teamIds.Contains(m.Team2.Id)))
            .ToListAsync();

        foreach (var match in unplayedMatches)
        {
            if (match.IsWalkover || match.WinnerTeamId != null || match.Winner != null ||
                (match.Sets != null && match.Sets.Any()))
            {
                continue;
            }

            match.IsWalkover = true;
            bool team1Withdrew = match.Team1 != null && teamIds.Contains(match.Team1.Id);

            match.WalkoverTeamId = team1Withdrew ? match.Team1?.Id : match.Team2?.Id;
            match.Winner = team1Withdrew ? match.Team2 : match.Team1;
            match.WinnerTeamId = team1Withdrew ? match.Team2?.Id : match.Team1?.Id;

            bracketEngine.AdvanceWinnerInBracket(db, match, emailSender);
        }

        var registration = await db.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.PlayerId == playerId);
        if (registration != null) registration.HasWithdrawn = true;

        await db.SaveChangesAsync();
        return (true, "Spieler wurde abgemeldet. Alle seine offenen Spiele wurden automatisch als w.o. für die Gegner gewertet.");
    }

    public async Task DeleteTeamAsync(int teamId)
    {
        var team = await db.Teams
            .Include(t => t.TeamPlayers)
            .FirstOrDefaultAsync(t => t.Id == teamId);

        if (team == null) return;

        var knockoutMatches = await db.KnockoutMatch
            .Where(m => (m.Team1 != null && m.Team1.Id == teamId) || (m.Team2 != null && m.Team2.Id == teamId))
            .ToListAsync();

        foreach (var m in knockoutMatches)
        {
            if (m.Team1?.Id == teamId) m.Team1 = null;
            if (m.Team2?.Id == teamId) m.Team2 = null;
        }

        var groupMatches = await db.Matches
            .Where(m => (m.Team1 != null && m.Team1.Id == teamId) || (m.Team2 != null && m.Team2.Id == teamId))
            .ToListAsync();

        db.Matches.RemoveRange(groupMatches);

        var groupTeams = await db.GroupTeams
            .Where(gt => gt.TeamId == teamId)
            .ToListAsync();

        db.GroupTeams.RemoveRange(groupTeams);
        db.TeamPlayer.RemoveRange(team.TeamPlayers);
        db.Teams.Remove(team);

        await db.SaveChangesAsync();
    }
}
