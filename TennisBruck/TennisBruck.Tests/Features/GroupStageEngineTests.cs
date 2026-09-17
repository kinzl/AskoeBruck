using TennisBruck.Features.Championship;
using TennisDb;
using Match = TennisDb.Match;
using Xunit;

namespace TennisBruck.Tests.Features;

public class GroupStageEngineTests
{
    private readonly GroupStageEngine _engine = new();

    [Fact]
    public void CalculateGroupTable_RegularPlayedMatch_ComputesPointsSetsGamesAndRanksCorrectly()
    {
        // Arrange
        var team1 = new Team { Id = 1 };
        var team2 = new Team { Id = 2 };

        var groupTeam1 = new GroupTeam { GroupId = 1, TeamId = 1, Team = team1 };
        var groupTeam2 = new GroupTeam { GroupId = 1, TeamId = 2, Team = team2 };

        var match = new Match
        {
            Id = 1,
            Team1 = team1,
            Team2 = team2,
            Sets = new List<Set>
            {
                new() { SetNumber = 1, Player1GamesWon = 6, Player2GamesWon = 2 },
                new() { SetNumber = 2, Player1GamesWon = 6, Player2GamesWon = 4 }
            }
        };

        // Act
        var result = _engine.CalculateGroupTable(new[] { groupTeam1, groupTeam2 }, new[] { match });

        // Assert
        Assert.Equal(2, result.Count);

        var first = result[0];
        Assert.Equal(1, first.GroupTeam.TeamId);
        Assert.Equal(1, first.Points);
        Assert.Equal(2, first.SetsWon);
        Assert.Equal(0, first.SetsLost);
        Assert.Equal(2, first.SetDifference);
        Assert.Equal(12, first.GamesWon);
        Assert.Equal(6, first.GamesLost);
        Assert.Equal(6, first.GameDifference);

        var second = result[1];
        Assert.Equal(2, second.GroupTeam.TeamId);
        Assert.Equal(0, second.Points);
        Assert.Equal(0, second.SetsWon);
        Assert.Equal(2, second.SetsLost);
        Assert.Equal(-2, second.SetDifference);
        Assert.Equal(6, second.GamesWon);
        Assert.Equal(12, second.GamesLost);
        Assert.Equal(-6, second.GameDifference);
    }

    [Fact]
    public void CalculateGroupTable_WalkoverMatch_Awards12GamesAnd2SetsToWinner()
    {
        // Arrange
        var team1 = new Team { Id = 1 };
        var team2 = new Team { Id = 2 };

        var groupTeam1 = new GroupTeam { GroupId = 1, TeamId = 1, Team = team1 };
        var groupTeam2 = new GroupTeam { GroupId = 1, TeamId = 2, Team = team2 };

        // Team 2 gave walkover, so team 1 is the winner
        var match = new Match
        {
            Id = 1,
            Team1 = team1,
            Team2 = team2,
            IsWalkover = true,
            WalkoverTeamId = 2,
            Winner = team1
        };

        // Act
        var result = _engine.CalculateGroupTable(new[] { groupTeam1, groupTeam2 }, new[] { match });

        // Assert
        var winnerEntry = result.Single(r => r.GroupTeam.TeamId == 1);
        Assert.Equal(1, winnerEntry.Points);
        Assert.Equal(2, winnerEntry.SetsWon);
        Assert.Equal(0, winnerEntry.SetsLost);
        Assert.Equal(12, winnerEntry.GamesWon);
        Assert.Equal(0, winnerEntry.GamesLost);

        var loserEntry = result.Single(r => r.GroupTeam.TeamId == 2);
        Assert.Equal(0, loserEntry.Points);
        Assert.Equal(0, loserEntry.SetsWon);
        Assert.Equal(2, loserEntry.SetsLost);
        Assert.Equal(0, loserEntry.GamesWon);
        Assert.Equal(12, loserEntry.GamesLost);
    }

    [Fact]
    public void CalculateGroupTable_TieBreakOrdering_SortsByPointsThenSetDiffThenGameDiff()
    {
        // Arrange
        var teamA = new Team { Id = 1 };
        var teamB = new Team { Id = 2 };
        var teamC = new Team { Id = 3 };

        var gtA = new GroupTeam { GroupId = 1, TeamId = 1, Team = teamA };
        var gtB = new GroupTeam { GroupId = 1, TeamId = 2, Team = teamB };
        var gtC = new GroupTeam { GroupId = 1, TeamId = 3, Team = teamC };

        // Match 1: A beats B: 2-0 sets, 12-4 games (+2 set diff, +8 games)
        var match1 = new Match
        {
            Id = 1,
            Team1 = teamA,
            Team2 = teamB,
            Sets = new List<Set>
            {
                new() { SetNumber = 1, Player1GamesWon = 6, Player2GamesWon = 2 },
                new() { SetNumber = 2, Player1GamesWon = 6, Player2GamesWon = 2 }
            }
        };

        // Match 2: B beats C: 2-1 sets, 14-12 games (+1 set diff, +2 games)
        var match2 = new Match
        {
            Id = 2,
            Team1 = teamB,
            Team2 = teamC,
            Sets = new List<Set>
            {
                new() { SetNumber = 1, Player1GamesWon = 6, Player2GamesWon = 4 },
                new() { SetNumber = 2, Player1GamesWon = 2, Player2GamesWon = 6 },
                new() { SetNumber = 3, Player1GamesWon = 6, Player2GamesWon = 2 }
            }
        };

        // Match 3: C beats A: 2-0 sets, 12-8 games (+2 set diff, +4 games)
        var match3 = new Match
        {
            Id = 3,
            Team1 = teamC,
            Team2 = teamA,
            Sets = new List<Set>
            {
                new() { SetNumber = 1, Player1GamesWon = 6, Player2GamesWon = 4 },
                new() { SetNumber = 2, Player1GamesWon = 6, Player2GamesWon = 4 }
            }
        };

        // All have 1 point.
        // A: sets won 2, lost 2 => diff 0. Games: won 20, lost 16 => diff +4
        // B: sets won 2, lost 3 => diff -1.
        // C: sets won 3, lost 2 => diff +1. Games: won 24, lost 22 => diff +2

        // Act
        var result = _engine.CalculateGroupTable(new[] { gtA, gtB, gtC }, new[] { match1, match2, match3 });

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result[0].GroupTeam.TeamId); // C has +1 set diff
        Assert.Equal(1, result[1].GroupTeam.TeamId); // A has 0 set diff
        Assert.Equal(2, result[2].GroupTeam.TeamId); // B has -1 set diff
    }
}
