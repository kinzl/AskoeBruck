namespace TennisDb;

public partial class TeamPlayer
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; }

    public int PlayerId { get; set; }
    public Player Player { get; set; }
}