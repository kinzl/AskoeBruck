using Microsoft.EntityFrameworkCore;

namespace TennisDb;

public class TennisContext : DbContext
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
    public DbSet<PlayerCourtGrieskirchen> PlayerCourtGrieskirchen { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<Court> Court { get; set; }
    public DbSet<RegistrationVerification> RegistrationVerifications { get; set; }
    
}