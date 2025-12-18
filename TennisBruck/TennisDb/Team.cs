namespace TennisDb;

public partial class Team
{
    public int Id { get; set; }

    public int CompetitionId { get; set; }
    public Competition Competition { get; set; }

    public int BracketNo { get; set; }
    public ICollection<TeamPlayer> Players { get; set; } = new List<TeamPlayer>();
}
