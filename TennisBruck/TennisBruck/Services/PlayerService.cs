using TennisDb;

namespace TennisBruck.Services;

public class PlayerService
{
    private Player? _player;
    private TennisContext _db;

    public PlayerService(IServiceProvider serviceProvider)
    {
        var scope = serviceProvider.CreateScope();
        _db = scope.ServiceProvider.GetRequiredService<TennisContext>();
    }

    public Player? GetPlayer(string? sessionName)
    {
        if (_player != null) return _player;
        if (sessionName != null) _player = _db.Players.Single(x => x.Id == int.Parse(sessionName));
        else _player = null;

        return _player;
    }

    public void SetPlayer(Player? player)
    {
        _player = player;
    }
    
}