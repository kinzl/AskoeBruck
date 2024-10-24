using Microsoft.EntityFrameworkCore;
using TennisBruck.Extensions;
using TennisDb;

namespace TennisBruck.Services;

public class StartupBackgroundService : BackgroundService
{
    private readonly IServiceScope _scope;
    private PasswordEncryption _pe;

    public StartupBackgroundService(IServiceProvider provider, PasswordEncryption pe)
    {
        _pe = pe;
        _scope = provider.CreateScope();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("ExecuteAsync STARTUPSERVICE");
        var db = _scope.ServiceProvider.GetRequiredService<TennisContext>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        Seed(db);

        return Task.Run(() => db.SaveChanges(), stoppingToken);
    }

    private void Seed(TennisContext db)
    {
        db.Players.Add(new Player()
        {
            Firstname = "Alice",
            Lastname = "Smith",
            PasswordHash = _pe.HashPassword("1234"),
            EmailOrPhone = "asmi@gmail.com",
            Username = "asmith",
            IsAdmin = false
        });

        db.Players.Add(new Player()
        {
            Firstname = "Emil",
            Lastname = "Kinzl",
            PasswordHash = _pe.HashPassword("1234"),
            EmailOrPhone = "ekin@gmail.com",
            Username = "kinzle",
            IsAdmin = true
        });
        db.SaveChanges();
    }
}