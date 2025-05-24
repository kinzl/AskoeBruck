namespace TennisDb;

public class Group
{
    public int Id { get; set; }
    public string GroupName { get; set; }
    public int MaxAmount { get; set; }
    public Competition Competition { get; set; }
    public List<GroupPlayer> GroupPlayers { get; set; }
}