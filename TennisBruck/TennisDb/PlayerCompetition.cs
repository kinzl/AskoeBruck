namespace TennisDb;

public class PlayerCompetition
{
    public int Id { get; set; }
    public DateTime Year { get; set; }
    public Player Player { get; set; }
    public Competition Competition { get; set; }
}