namespace TennisDb;

public class PlayerCompetition
{
    public int Id { get; set; }
    public int BracketNo { get; set; }
    public DateTime Year { get; set; }
    public Player SinglePlayer { get; set; }
    public Player? DoublePlayer { get; set; }
    public Competition Competition { get; set; }
    public Player Registered { get; set; }
}