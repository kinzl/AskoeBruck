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

    // protected override async Task<Task<int>> ExecuteAsync(CancellationToken stoppingToken)
    // {
    //     Console.WriteLine("ExecuteAsync STARTUPSERVICE");
    //     var db = _scope.ServiceProvider.GetRequiredService<TennisContext>();
    //
    //     await db.Database.EnsureDeletedAsync(stoppingToken);
    //     await db.Database.EnsureCreatedAsync(stoppingToken);
    //     SeedPlayer(db);
    //
    //     return db.SaveChangesAsync(stoppingToken);
    // }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("ExecuteAsync STARTUPSERVICE");
        var db = _scope.ServiceProvider.GetRequiredService<TennisContext>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        SeedPlayer(db);
        SeedCompetition(db);

        db.SaveChanges();

        return Task.CompletedTask;
    }

    private void SeedPlayer(TennisContext db)
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
            Firstname = "Max",
            Lastname = "Kammerer",
            PasswordHash = _pe.HashPassword("1234"),
            EmailOrPhone = "kammerem@gmail.com",
            Username = "kammerem",
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
            IsAdmin = false
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

    private void SeedCompetition(TennisContext db)
    {
        db.Competitions.Add(new Competition()
        {
            Name = "Herren Einzel"
        });
        db.Competitions.Add(new Competition()
        {
            Name = "Herren Doppel"
        });
        db.SaveChanges();
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            Player = db.Players.First(x => x.Username == "asmith"),
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            Player = db.Players.First(x => x.Username == "asmith"),
            Competition = db.Competitions.First(x => x.Name == "Herren Doppel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            Player = db.Players.First(x => x.Username == "kammerem"),
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            Player = db.Players.First(x => x.Username == "kammerem"),
            Competition = db.Competitions.First(x => x.Name == "Herren Doppel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            Player = db.Players.First(x => x.Username == "kinzle"),
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            Player = db.Players.First(x => x.Username == "kinzle"),
            Competition = db.Competitions.First(x => x.Name == "Herren Doppel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            Player = db.Players.First(x => x.Username == "EckerS"),
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            Player = db.Players.First(x => x.Username == "EckerS"),
            Competition = db.Competitions.First(x => x.Name == "Herren Doppel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            Player = db.Players.First(x => x.Username == "WimmerG"),
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            Player = db.Players.First(x => x.Username == "ReppB"),
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        });
    }
}