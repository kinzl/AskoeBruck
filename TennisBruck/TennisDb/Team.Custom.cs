namespace TennisDb;

public partial class Team
{
    public string PlayersToString()
    {
        return $"{string.Join("/", Players.Select(x => x.Player.Username))}";
    }
}