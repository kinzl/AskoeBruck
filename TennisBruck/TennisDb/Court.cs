namespace TennisDb;

public class Court
{
    public int Id { get; set; }
    public DateTime MatchDay { get; set; }
    public DateTime? MatchTime { get; set; }
    public List<PlayerCourtGrieskirchen> PlayerCourtGrieskirchens { get; set; }
}