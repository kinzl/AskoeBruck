namespace TennisDb;

public class Set
{
    public int Id { get; set; }
    public int SetNumber { get; set; }
    public int Player1GamesWon { get; set; }
    public int Player2GamesWon { get; set; }
    public Match Match { get; set; }
}