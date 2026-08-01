namespace TennisDb;

public class PyramidChallenge
{
    public int Id { get; set; }
    public int CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;
    public int ChallengerTeamId { get; set; }
    public Team ChallengerTeam { get; set; } = null!;
    public int DefenderTeamId { get; set; }
    public Team DefenderTeam { get; set; } = null!;
    public DateTime ChallengeDate { get; set; } = DateTime.UtcNow;
    public DateTime? MatchDate { get; set; }
    public int Status { get; set; } // 0 = Pending, 1 = Completed, 2 = Cancelled
    public int? WinnerTeamId { get; set; }
    public Team? WinnerTeam { get; set; }
    public string? Score { get; set; }
}
