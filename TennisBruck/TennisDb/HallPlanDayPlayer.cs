namespace TennisDb;

public class HallPlanDayPlayer
{
    public int Id { get; set; }

    public int HallPlanDayId { get; set; }
    public HallPlanDay HallPlanDay { get; set; } = null!;

    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;
}