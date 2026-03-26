namespace TennisDb;

public partial class Team
{
    public int Id { get; set; }

    public int CompetitionId { get; set; }

    public int BracketNo { get; set; }
    public Competition Competition { get; set; }
    public ICollection<TeamPlayer> TeamPlayers { get; set; } = new List<TeamPlayer>();
}