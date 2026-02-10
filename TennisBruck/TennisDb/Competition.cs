namespace TennisDb;

public class Competition
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsSingle { get; set; }
    public DateTime RegistrationUntil { get; set; }
    public List<Team> Teams { get; set; }
    public List<Group> Groups { get; set; }
}