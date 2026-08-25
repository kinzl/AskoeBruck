using TennisDb;
using Xunit;

namespace TennisBruck.Tests;

public class MatchScoreTests
{
    [Fact]
    public void SetToString_OrdersSetsBySetNumber()
    {
        // Arrange
        var match = new Match
        {
            Sets = new List<Set>
            {
                new Set { SetNumber = 2, Player1GamesWon = 4, Player2GamesWon = 6 },
                new Set { SetNumber = 3, Player1GamesWon = 10, Player2GamesWon = 12 },
                new Set { SetNumber = 1, Player1GamesWon = 6, Player2GamesWon = 4 }
            }
        };

        // Act
        var result = match.SetToString();

        // Assert
        Assert.Equal("6-4 4-6 10-12", result);
    }

    [Fact]
    public void Sets_DisplayScore_MaintainsSetNumberOrder()
    {
        // Arrange - Unordered sets as might be returned from EF query without explicit ORDER BY
        var sets = new List<Set>
        {
            new Set { SetNumber = 2, Player1GamesWon = 4, Player2GamesWon = 6 },
            new Set { SetNumber = 3, Player1GamesWon = 10, Player2GamesWon = 12 },
            new Set { SetNumber = 1, Player1GamesWon = 6, Player2GamesWon = 4 }
        };

        // Act
        var team1Score = string.Join(" ", sets.OrderBy(s => s.SetNumber).Select(s => s.Player1GamesWon));
        var team2Score = string.Join(" ", sets.OrderBy(s => s.SetNumber).Select(s => s.Player2GamesWon));

        // Assert
        Assert.Equal("6 4 10", team1Score);
        Assert.Equal("4 6 12", team2Score);
    }
}
