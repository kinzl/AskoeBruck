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

    public bool IsDouble { get; set; } = false;

    // Wie viele Spieler werden insgesamt noch gesucht? (1 bis 3)
    public int NeededPlayers { get; set; } = 1;

    // Player 2 (Einzel-Gegner oder Doppel-Partner)
    public int? MatchedWithPlayerId { get; set; }
    [ForeignKey("MatchedWithPlayerId")] public virtual Player? MatchedWithPlayer { get; set; }

    // Player 3 (Gegner 1 beim Doppel)
    public int? MatchedWithPlayer2Id { get; set; }
    [ForeignKey("MatchedWithPlayer2Id")] public virtual Player? MatchedWithPlayer2 { get; set; }

    // Player 4 (Gegner 2 beim Doppel)
    public int? MatchedWithPlayer3Id { get; set; }
    [ForeignKey("MatchedWithPlayer3Id")] public virtual Player? MatchedWithPlayer3 { get; set; }
}