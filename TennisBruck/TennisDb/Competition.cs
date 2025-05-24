namespace TennisDb;

public class Competition
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<PlayerCompetition> PlayerCompetitions { get; set; }
}