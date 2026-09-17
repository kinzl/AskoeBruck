namespace TennisDb;

public class OetvMatch
{
    public int Id { get; set; }

    /// <summary>
    /// Name of the official ÖTV competition/league, e.g. "OÖTV Mannschaftsmeisterschaft 2026 - 1. Klasse West"
    /// </summary>
    public string CompetitionName { get; set; } = string.Empty;

    /// <summary>
    /// Category / age group: "Herren", "Damen", "Kids U10", "Jugend U12", "Senioren 35+", etc.
    /// </summary>
    public string Category { get; set; } = "Herren";

    /// <summary>
    /// Home team name, e.g. "ASKÖ Bruck/Peuerbach 1"
    /// </summary>
    public string HomeTeam { get; set; } = string.Empty;

    /// <summary>
    /// Away team name, e.g. "UTC Peuerbach 1"
    /// </summary>
    public string AwayTeam { get; set; } = string.Empty;

    /// <summary>
    /// True if ASKÖ Bruck is playing at home
    /// </summary>
    public bool IsHomeMatch { get; set; } = true;

    /// <summary>
    /// Exact match date and starting time
    /// </summary>
    public DateTime MatchDateTime { get; set; }

    /// <summary>
    /// Venue / Place of the match (e.g. "Tennisanlage ASKÖ Bruck" or opponent's venue)
    /// </summary>
    public string Venue { get; set; } = "Tennisanlage ASKÖ Bruck";

    /// <summary>
    /// Status: "Geplant", "Beendet", "Verschoben", "Abgesagt"
    /// </summary>
    public string Status { get; set; } = "Geplant";

    /// <summary>
    /// Match result / score, e.g. "6 : 3" or null if upcoming
    /// </summary>
    public string? Score { get; set; }

    /// <summary>
    /// Optional direct link to official ÖTV match sheet / Spielbericht on oetv.at
    /// </summary>
    public string? OetvUrl { get; set; }

    /// <summary>
    /// Optional internal notes (e.g. "Treffpunkt 12:00 Uhr", "Platz 1-3 reserviert")
    /// </summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
