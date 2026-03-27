namespace TennisDb;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class AvailabilitySlot
{
    [Key] public int Id { get; set; }

    // Wir nennen es jetzt PlayerId (nicht mehr UserId)
    [Required] public int PlayerId { get; set; }

    // Hier verknüpfen wir exakt deine Player-Klasse
    [ForeignKey("PlayerId")] public virtual Player Player { get; set; }

    [Required] public DateTime Date { get; set; }

    [Required] public TimeSpan StartTime { get; set; }

    [Required] public TimeSpan EndTime { get; set; }

    // Das Fragezeichen schützt uns vor dem Null-Fehler!
    [MaxLength(200)] public string? Message { get; set; }

    public bool IsMatched { get; set; } = false;

    // Auch hier konsequent: PlayerId statt UserId
    public int? MatchedWithPlayerId { get; set; }
}