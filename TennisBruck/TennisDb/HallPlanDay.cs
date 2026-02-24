namespace TennisDb;

public class HallPlanDay
{
    public int Id { get; set; }

    public int HallPlanId { get; set; }
    public HallPlanEntity HallPlanEntity { get; set; } = null!;

    public DateTime PlayDate { get; set; }

    public ICollection<HallPlanDayPlayer> Players { get; set; } = new List<HallPlanDayPlayer>();
}