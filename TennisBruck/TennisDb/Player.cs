using Microsoft.AspNetCore.Identity;

namespace TennisDb;

public partial class Player
{
    public int Id { get; set; }
    public string Firstname { get; set; }
    public string Lastname { get; set; }
    public List<HallEntity> HallEntities { get; set; }
    public List<TournamentRegistration> TournamentRegistrations { get; set; }
    public IdentityUser? IdentityUser { get; set; }
    public string? IdentityUserId { get; set; }
    public decimal? Itn { get; set; }
    public DateTime? LastItnUpdate { get; set; }
    public string? NuLigaPlayerUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public PlayerNotificationSettings? NotificationSettings { get; set; }
}