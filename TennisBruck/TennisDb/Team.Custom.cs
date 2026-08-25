namespace TennisDb;

public partial class Team
{
    public string PlayersToString()
    {
        return $"{string.Join("/", TeamPlayers.OrderBy(tp => tp.Player?.Lastname).ThenBy(tp => tp.Player?.Firstname).Select(x => x.Player?.ToString() ?? "?"))}";
    }

    public string PlayersToStringWithItn()
    {
        return $"{string.Join("/", TeamPlayers.OrderBy(tp => tp.Player?.Lastname).ThenBy(tp => tp.Player?.Firstname).Select(x => x.Player?.ToStringWithItn() ?? "?"))}";
    }
}