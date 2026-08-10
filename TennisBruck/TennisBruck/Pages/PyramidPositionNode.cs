namespace TennisBruck.Pages;

public class PyramidPositionNode
{
    public required PyramidRank PyramidRank { get; set; }
    public PyramidChallenge? ActiveChallenge { get; set; }
    public bool IsMyTeam { get; set; }
    public bool CanBeChallengedByCurrentUser { get; set; }
}