namespace TennisDb;

public class HallPlanRegistration
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public int HallPlanId { get; set; }
    public HallPlanEntity HallPlanEntity { get; set; } = null!;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}