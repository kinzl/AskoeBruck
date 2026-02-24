namespace TennisDb;

public class HallEntity
{
    public int Id { get; set; }
    public Player Player { get; set; }
    public HallPlanDay HallPlanDay { get; set; }
    
}