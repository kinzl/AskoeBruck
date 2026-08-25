namespace TennisBruck.Pages;

public class GroupTableEntry
{
    // Wir merken uns das Original-Objekt für den Admin-Lösch-Button
    public GroupTeam GroupTeam { get; set; }
    public Team Team { get; set; }
    public int MatchesPlayed { get; set; }
    public int Points { get; set; }
    public int SetsWon { get; set; }
    public int SetsLost { get; set; }
    public int GamesWon { get; set; }

    public int GamesLost { get; set; }

    public int SetDifference => SetsWon - SetsLost;
    public int GameDifference => GamesWon - GamesLost;

}