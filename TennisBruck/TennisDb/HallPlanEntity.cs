namespace TennisDb;

public class HallPlanEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public ICollection<HallPlanRegistration> Registrations { get; set; } = new List<HallPlanRegistration>();
    public ICollection<HallPlanDay> Days { get; set; } = new List<HallPlanDay>();
}