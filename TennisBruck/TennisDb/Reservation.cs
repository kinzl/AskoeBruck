namespace TennisDb;
using System.ComponentModel.DataAnnotations.Schema;

public class Reservation
{
    public int Id { get; set; }
    public int CourtNumber { get; set; }
    public string? EventName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Player? Player { get; set; }

    [NotMapped]
    public string DisplayName 
    {
        get 
        {
            if (!string.IsNullOrWhiteSpace(EventName))
            {
                return EventName;
            }
            
            return Player != null ? Player.ToString() : "Unbekannt";
        }
    }
}