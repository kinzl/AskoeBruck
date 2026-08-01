namespace TennisDb;

public class PyramidRank
{
    public int Id { get; set; }
    public int CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;
    public int Rank { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
}
