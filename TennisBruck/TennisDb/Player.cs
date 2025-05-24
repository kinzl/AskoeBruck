namespace TennisDb;

public partial class Player
{
    public int Id { get; set; }
    public String Firstname { get; set; }
    public String Lastname { get; set; }
    public String EmailOrPhone { get; set; }
    public String PasswordHash { get; set; }
    public String Username { get; set; }
    public bool IsAdmin { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? TokenExpiry { get; set; }
    public bool IsPlayingGrieskirchen { get; set; }
    public List<PlayerCourtGrieskirchen> PlayerCourtGrieskirchen { get; set; }
    public List<GroupPlayer> GroupPlayers { get; set; }
}