namespace TennisDb;

public class TournamentRegistration
{
    public int Id { get; set; }

    public int CompetitionId { get; set; }
    public Competition Competition { get; set; }

    public int PlayerId { get; set; }
    public Player Player { get; set; }
    public DateTime RegisteredAt { get; set; }
}