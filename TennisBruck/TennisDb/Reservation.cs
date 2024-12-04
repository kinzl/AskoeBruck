namespace TennisDb;

public class Reservation
{
    public int Id { get; set; }
    public int CourtNumber { get; set; } // 1, 2, or 3
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Player Player { get; set; }
}
