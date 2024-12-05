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

    protected override async Task<Task<int>> ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("ExecuteAsync STARTUPSERVICE");
        var db = _scope.ServiceProvider.GetRequiredService<TennisContext>();

        await db.Database.EnsureDeletedAsync(stoppingToken);
        await db.Database.EnsureCreatedAsync(stoppingToken);
        Seed(db);

        return db.SaveChangesAsync(stoppingToken);
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
            IsPlayingGrieskirchen = false,
            IsAdmin = false
        });

        db.Players.Add(new Player()
        {
            Firstname = "Emil",
            Lastname = "Kinzl",
            PasswordHash = _pe.HashPassword("1234"),
            EmailOrPhone = "ekin@gmail.com",
            Username = "kinzle",
            IsPlayingGrieskirchen = true,
            IsAdmin = true
        });

        db.Players.Add(new Player()
        {
            Firstname = "Stefan",
            Lastname = "Ecker",
            PasswordHash = _pe.HashPassword("1234"),
            EmailOrPhone = "EckerStefan@gmail.com",
            Username = "EckerS",
            IsPlayingGrieskirchen = true,
            IsAdmin = true
        });

        db.Players.Add(new Player()
        {
            Firstname = "Gerald",
            Lastname = "Wimmer",
            PasswordHash = _pe.HashPassword("1234"),
            EmailOrPhone = "WimmerGerald@gmail.com",
            Username = "WimmerG",
            IsPlayingGrieskirchen = true,
            IsAdmin = true
        });

        db.Players.Add(new Player()
        {
            Firstname = "Bernhard",
            Lastname = "Repp",
            PasswordHash = _pe.HashPassword("1234"),
            EmailOrPhone = "ReppB@gmail.com",
            Username = "ReppB",
            IsPlayingGrieskirchen = true,
            IsAdmin = true
        });

        db.Players.Add(new Player()
        {
            Firstname = "Stefan",
            Lastname = "Hofer",
            PasswordHash = _pe.HashPassword("1234"),
            EmailOrPhone = "HoferS@gmail.com",
            Username = "HoferS",
            IsPlayingGrieskirchen = true,
            IsAdmin = true
        });
        db.SaveChanges();
    }
}