namespace TennisDb;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class AvailabilitySlot
{
    [Key] public int Id { get; set; }

    [Required] public int PlayerId { get; set; }

    [ForeignKey("PlayerId")] public virtual Player Player { get; set; }

    [Required] public DateTime Date { get; set; }

    [Required] public TimeSpan StartTime { get; set; }

    [Required] public TimeSpan EndTime { get; set; }
    [MaxLength(200)] public string? Message { get; set; }

    public bool IsMatched { get; set; }

    public bool IsDouble { get; set; }
    public int NeededPlayers { get; set; } = 1;
    public int? MatchedWithPlayerId { get; set; }
    [ForeignKey("MatchedWithPlayerId")] public virtual Player? MatchedWithPlayer { get; set; }
    public int? MatchedWithPlayer2Id { get; set; }
    [ForeignKey("MatchedWithPlayer2Id")] public virtual Player? MatchedWithPlayer2 { get; set; }
    public int? MatchedWithPlayer3Id { get; set; }
    [ForeignKey("MatchedWithPlayer3Id")] public virtual Player? MatchedWithPlayer3 { get; set; }
}