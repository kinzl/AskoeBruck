namespace TennisDb;

public class GroupPlayer
{
    public int Id { get; set; }
    public Player Player { get; set; }
    public Group Group { get; set; }
    public int PlayerId { get; set; }
    public int GroupId { get; set; }
}