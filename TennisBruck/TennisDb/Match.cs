namespace TennisDb;

public class Match
{
    public int Id { get; set; }
    public Team? Winner { get; set; }
    public int? WinnerTeamId { get; set; }
    public Team? Team1 { get; set; }
    public Team? Team2 { get; set; }
    public List<Set>? Sets { get; set; }
    public bool IsWalkover { get; set; } = false;
    public int? WalkoverTeamId { get; set; }
    public Group? Group { get; set; }

    public string SetToString()
    {
        return Sets.Aggregate("", (current, set) => current + $"{set.Player1GamesWon}-{set.Player2GamesWon} ");
    }
}