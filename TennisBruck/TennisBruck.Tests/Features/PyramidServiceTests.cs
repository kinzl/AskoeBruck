using TennisBruck.Features.Pyramid;
using TennisBruck.Features.Pyramid.Models;
using TennisDb;
using Xunit;

namespace TennisBruck.Tests.Features;

public class PyramidServiceTests
{
    [Fact]
    public void BuildPyramidLevels_BuildsCorrectPyramidHierarchyAndLevels()
    {
        // Arrange
        var service = new PyramidService(null!, null!);

        var ranks = new List<PyramidRank>
        {
            new() { Id = 1, TeamId = 10, Rank = 1, CompetitionId = 1 },
            new() { Id = 2, TeamId = 20, Rank = 2, CompetitionId = 1 },
            new() { Id = 3, TeamId = 30, Rank = 3, CompetitionId = 1 },
            new() { Id = 4, TeamId = 40, Rank = 4, CompetitionId = 1 },
            new() { Id = 5, TeamId = 50, Rank = 5, CompetitionId = 1 },
            new() { Id = 6, TeamId = 60, Rank = 6, CompetitionId = 1 }
        };

        var activeChallenges = new List<PyramidChallenge>();
        var myRank = ranks[3]; // Rank 4 (Team 40)
        var myTeam = new Team { Id = 40 };

        // Act
        var levels = service.BuildPyramidLevels(ranks, activeChallenges, myRank, myTeam);

        // Assert
        // Level 1: 1 node (Rank 1)
        // Level 2: 2 nodes (Rank 2, 3)
        // Level 3: 3 nodes (Rank 4, 5, 6)
        Assert.Equal(3, levels.Count);
        Assert.Single(levels[0].Nodes);
        Assert.Equal(2, levels[1].Nodes.Count);
        Assert.Equal(3, levels[2].Nodes.Count);

        // My rank is 4. Teams within 3 ranks above: Rank 3, Rank 2, Rank 1.
        // Rank 3 (diff = 1) -> can be challenged
        Assert.True(levels[1].Nodes[1].CanBeChallengedByCurrentUser);
        // Rank 2 (diff = 2) -> can be challenged
        Assert.True(levels[1].Nodes[0].CanBeChallengedByCurrentUser);
        // Rank 1 (diff = 3) -> can be challenged (<= 3)
        Assert.True(levels[0].Nodes[0].CanBeChallengedByCurrentUser);

        // Rank 4 is my team
        Assert.True(levels[2].Nodes[0].IsMyTeam);
        // Rank 5 is below me -> cannot be challenged
        Assert.False(levels[2].Nodes[1].CanBeChallengedByCurrentUser);
    }

    [Fact]
    public void BuildPyramidLevels_DisallowsChallenge_WhenTargetIsInActiveChallenge()
    {
        // Arrange
        var service = new PyramidService(null!, null!);

        var ranks = new List<PyramidRank>
        {
            new() { Id = 1, TeamId = 10, Rank = 1, CompetitionId = 1 },
            new() { Id = 2, TeamId = 20, Rank = 2, CompetitionId = 1 }
        };

        var activeChallenges = new List<PyramidChallenge>
        {
            new() { Id = 1, ChallengerTeamId = 10, DefenderTeamId = 99, Status = 0 }
        };

        var myRank = ranks[1]; // Rank 2
        var myTeam = new Team { Id = 20 };

        // Act
        var levels = service.BuildPyramidLevels(ranks, activeChallenges, myRank, myTeam);

        // Assert
        // Team 10 is in an active challenge -> cannot be challenged
        Assert.False(levels[0].Nodes[0].CanBeChallengedByCurrentUser);
    }
}
