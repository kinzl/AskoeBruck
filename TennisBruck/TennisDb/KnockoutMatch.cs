namespace TennisDb;

public class KnockoutMatch
{
    public int Id { get; set; }
    public Player? Winner { get; set; }
    public Player? Player1 { get; set; }
    public Player? DoublePlayer1 { get; set; }
    public Player? Player2 { get; set; }
    public Player? DoublePlayer2 { get; set; }
    public List<Set>? Sets { get; set; }
    public int BracketNo { get; set; }

    public int RoundNo { get; set; }
    public int? NextGame { get; set; }
    public bool IsBye { get; set; }

    public string SetToString()
    {
        return Sets.Aggregate("", (current, set) => current + $"{set.Player1GamesWon}-{set.Player2GamesWon} ");
    }
}