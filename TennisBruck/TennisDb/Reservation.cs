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
    public int? PartnerId { get; set; }
    public Player? Partner { get; set; }

    [NotMapped]
    public string DisplayName 
    {
        get 
        {
            if (!string.IsNullOrWhiteSpace(EventName))
            {
                return EventName;
            }
            
            if (Player != null && Partner != null)
            {
                return $"{Player.Firstname} {Player.Lastname} / {Partner.Firstname} {Partner.Lastname}";
            }

            return Player != null ? Player.ToString() : "Unbekannt";
        }
    }
}