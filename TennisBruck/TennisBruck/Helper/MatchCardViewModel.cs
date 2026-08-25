namespace TennisBruck.Pages;

public class MatchCardViewModel
{
    public int MatchId { get; set; }
    public string Team1Name { get; set; } = string.Empty;
    public string Team2Name { get; set; } = string.Empty;
    public string? Team1Score { get; set; }
    public string? Team2Score { get; set; }
    public bool IsTeam1Winner { get; set; }
    public bool IsTeam2Winner { get; set; }
    public bool IsWalkover { get; set; }
    public int? WalkoverTeamId { get; set; }
    public int? Team1Id { get; set; }
    public int? Team2Id { get; set; }
    public string BadgeText { get; set; } = string.Empty;
    public string? DataGroup { get; set; }

    public bool IsDecided { get; set; }
    public bool IsMyMatch { get; set; }
    public bool IsAdmin { get; set; }
    public bool CanDelete { get; set; }

    public string DeleteHandler { get; set; } = "DeleteMatch";
    public string DeleteButtonText { get; set; } = "Löschen";
    public string SaveHandler { get; set; } = "SaveMatch";
    public string WalkoverHandler { get; set; } = "AdminWalkover";
    public string GiveWalkoverHandler { get; set; } = "GiveWalkover";

    public bool UseResultModal { get; set; }
}