namespace TennisDb;

public partial class Team
{
    public string PlayersToString()
    {
        return $"{string.Join("/", TeamPlayers.Select(x => x.Player?.ToString() ?? "?"))}";
    }

    public string PlayersToStringWithItn()
    {
        return $"{string.Join("/", TeamPlayers.Select(x => x.Player?.ToStringWithItn() ?? "?"))}";
    }
}