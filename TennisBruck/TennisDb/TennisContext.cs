using Microsoft.EntityFrameworkCore;

namespace TennisDb;

public class TennisContext : DbContext
{
    public TennisContext(DbContextOptions<TennisContext> options)
        : base(options)
    {
    }

    public TennisContext()
    {
    }

    public DbSet<Player> Players { get; set; }
}