using Microsoft.EntityFrameworkCore;
using TennisBruck.Dto;
using TennisDb;
using Group = TennisDb.Group;
using Match = TennisDb.Match;

namespace TennisBruck.Features.Championship;

public class GroupStageEngine
{
    public List<GroupTableEntry> CalculateGroupTable(
        IEnumerable<GroupTeam> groupTeams,
        IEnumerable<Match> groupMatches)
    {
        var table = new List<GroupTableEntry>();

        foreach (var groupTeam in groupTeams)
        {
            var entry = new GroupTableEntry { GroupTeam = groupTeam };

            var teamMatches = groupMatches.Where(m =>
                (m.Team1 != null && m.Team1.Id == groupTeam.TeamId) ||
                (m.Team2 != null && m.Team2.Id == groupTeam.TeamId)).ToList();

            var validMatches = teamMatches.Where(m => m.Team1 != null && m.Team2 != null).ToList();
            entry.MatchesPlayed = validMatches.Count;

            foreach (var match in validMatches)
            {
                int team1Id = match.Team1!.Id;
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

                    foreach (var set in match.Sets.OrderBy(s => s.SetNumber))
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

    public async Task<(bool Success, string Message)> GenerateGroupsAsync(
        TennisContext db,
        int competitionId,
        int targetGroupSize)
    {
        var comp = await db.Competitions.FirstOrDefaultAsync(c => c.Id == competitionId);
        if (comp == null) return (false, "Bewerb nicht gefunden.");

        if (comp.IsSingle)
        {
            var registeredPlayerIds = await db.TournamentRegistrations
                .Where(tr => tr.CompetitionId == competitionId && !tr.HasWithdrawn)
                .Select(tr => tr.PlayerId)
                .ToListAsync();

            var existingTeamPlayerIds = await db.Teams
                .Where(t => t.CompetitionId == competitionId)
                .SelectMany(t => t.TeamPlayers.Select(tp => tp.PlayerId))
                .ToListAsync();

            var missingPlayerIds = registeredPlayerIds.Except(existingTeamPlayerIds).ToList();
            if (missingPlayerIds.Any())
            {
                foreach (var pId in missingPlayerIds)
                {
                    db.Teams.Add(new Team
                    {
                        CompetitionId = competitionId,
                        TeamPlayers = new List<TeamPlayer> { new() { PlayerId = pId } }
                    });
                }

                await db.SaveChangesAsync();
            }
        }

        var shuffledPlayers = await db.Teams
            .Where(x => x.CompetitionId == competitionId)
            .ToListAsync();

        if (!shuffledPlayers.Any())
        {
            return (false, "Fehler: Keine Teams/Spieler für diesen Bewerb vorhanden. Generiere zuerst Teams oder füge Spieler hinzu.");
        }

        var oldGroups = await db.Groups
            .Include(g => g.GroupTeams)
            .Include(g => g.Matches)
            .Where(g => g.CompetitionId == competitionId)
            .ToListAsync();

        db.Matches.RemoveRange(oldGroups.SelectMany(x => x.Matches).ToList());
        db.GroupTeams.RemoveRange(oldGroups.SelectMany(gt => gt.GroupTeams).ToList());
        db.Groups.RemoveRange(oldGroups);

        await db.SaveChangesAsync();

        int numberOfGroups = (int)Math.Ceiling((double)shuffledPlayers.Count / targetGroupSize);

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

        return (true, "Gruppen wurden erstellt");
    }
}
