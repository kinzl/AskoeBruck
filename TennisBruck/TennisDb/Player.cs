namespace TennisDb;

public partial class Player
{
    public int Id { get; set; }
    public String Firstname { get; set; }
    public String Lastname { get; set; }
    public String Email { get; set; }
    public String Phone { get; set; }
    public String PasswordHash { get; set; }
    public String Username { get; set; }
    public bool IsAdmin { get; set; }
}