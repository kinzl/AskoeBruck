using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TennisDb;

public class TennisContext : IdentityDbContext<IdentityUser>, IDataProtectionKeyContext
{
    public TennisContext(DbContextOptions<TennisContext> options)
        : base(options)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public TennisContext()
    {
    }

    public DbSet<Player> Players { get; set; }
    public DbSet<HallEntity> HallEntities { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<HallPlanDay> Court { get; set; }
    public DbSet<Competition> Competitions { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<Match> Matches { get; set; }
    public DbSet<Set> Sets { get; set; }
    public DbSet<KnockoutMatch> KnockoutMatch { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<TeamPlayer> TeamPlayer { get; set; }
    public DbSet<TournamentRegistration> TournamentRegistrations { get; set; }
    public DbSet<GroupTeam> GroupTeams { get; set; }
    public DbSet<HallPlanEntity> HallPlanEntities { get; set; }
    public DbSet<HallPlanDay> HallPlanDays { get; set; }
    public DbSet<HallPlanRegistration> HallPlanRegistrations { get; set; }
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
    public DbSet<AvailabilitySlot> AvailabilitySlots { get; set; }
}