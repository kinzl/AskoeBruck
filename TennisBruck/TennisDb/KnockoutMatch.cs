namespace TennisDb;

public class KnockoutMatch : Match
{
    public int BracketNo { get; set; }

    public int RoundNo { get; set; }
    public int? NextGame { get; set; }
    public bool IsBye { get; set; }
    public Competition Competition { get; set; }
    public int CompetitionId { get; set; }
    public string PhaseName { get; set; } = "A-Bewerb";

    public string SetToString()
    {
        return Sets.Aggregate("", (current, set) => current + $"{set.Player1GamesWon}-{set.Player2GamesWon} ");
    }
}