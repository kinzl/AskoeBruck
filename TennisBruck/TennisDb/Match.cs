namespace TennisDb;

public class Match
{
    public int Id { get; set; }
    public Player? Winner { get; set; }
    public Player Player1 { get; set; }
    public Player Player2 { get; set; }
    public List<Set> Sets { get; set; }
    public Group Group { get; set; }

    public string SetToString()
    {
        return Sets.Aggregate("", (current, set) => current + $"{set.Player1GamesWon}-{set.Player2GamesWon} ");
    }
}