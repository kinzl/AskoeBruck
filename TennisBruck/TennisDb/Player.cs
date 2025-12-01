namespace TennisDb;

public partial class Player
{
    public int Id { get; set; }
    public string Firstname { get; set; }
    public string Lastname { get; set; }
    public string EmailOrPhone { get; set; }
    public string PasswordHash { get; set; }
    public string Username { get; set; }
    public bool IsAdmin { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? TokenExpiry { get; set; }
    public bool IsPlayingGrieskirchen { get; set; }
    public List<PlayerCourtGrieskirchen> PlayerCourtGrieskirchen { get; set; }
    public List<GroupPlayer> GroupPlayers { get; set; }
}