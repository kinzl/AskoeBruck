using TennisBruck.Extensions;
using TennisDb;

namespace TennisBruck.Services;

public class StartupBackgroundService(IServiceProvider provider, PasswordEncryption pe) : BackgroundService
{
    private readonly IServiceScope _scope = provider.CreateScope();

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("ExecuteAsync STARTUP SERVICE");
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
            PasswordHash = pe.HashPassword("1234"),
            EmailOrPhone = "asmi@gmail.com",
            Username = "asmith",
            IsPlayingGrieskirchen = false,
            IsAdmin = false
        });

        db.Players.Add(new Player()
        {
            Firstname = "Max",
            Lastname = "Kammerer",
            PasswordHash = pe.HashPassword("1234"),
            EmailOrPhone = "kammerem@gmail.com",
            Username = "kammerem",
            IsPlayingGrieskirchen = false,
            IsAdmin = false
        });

        db.Players.Add(new Player()
        {
            Firstname = "Emil",
            Lastname = "Kinzl",
            PasswordHash = pe.HashPassword("1234"),
            EmailOrPhone = "ekin@gmail.com",
            Username = "kinzle",
            IsPlayingGrieskirchen = true,
            IsAdmin = true
        });

        db.Players.Add(new Player()
        {
            Firstname = "Stefan",
            Lastname = "Ecker",
            PasswordHash = pe.HashPassword("1234"),
            EmailOrPhone = "EckerStefan@gmail.com",
            Username = "EckerS",
            IsPlayingGrieskirchen = true,
            IsAdmin = true
        });

        db.Players.Add(new Player()
        {
            Firstname = "Gerald",
            Lastname = "Wimmer",
            PasswordHash = pe.HashPassword("1234"),
            EmailOrPhone = "WimmerGerald@gmail.com",
            Username = "WimmerG",
            IsPlayingGrieskirchen = true,
            IsAdmin = true
        });

        db.Players.Add(new Player()
        {
            Firstname = "Bernhard",
            Lastname = "Repp",
            PasswordHash = pe.HashPassword("1234"),
            EmailOrPhone = "ReppB@gmail.com",
            Username = "ReppB",
            IsPlayingGrieskirchen = true,
            IsAdmin = false
        });

        db.Players.Add(new Player()
        {
            Firstname = "Stefan",
            Lastname = "Hofer",
            PasswordHash = pe.HashPassword("1234"),
            EmailOrPhone = "HoferS@gmail.com",
            Username = "HoferS",
            IsPlayingGrieskirchen = true,
            IsAdmin = true
        });
        db.SaveChanges();
    }

    private void SeedCompetition(TennisContext db)
    {
        db.Competitions.Add(new Competition
        {
            Name = "Herren Einzel",
            IsSingle = true,
            PlayerCompetitions = []
        });
        db.Competitions.Add(new Competition
        {
            Name = "Herren Doppel",
            IsSingle = false,
            PlayerCompetitions = []
        });
        db.SaveChanges();
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            SinglePlayer = db.Players.First(x => x.Username == "asmith"),
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            SinglePlayer = db.Players.First(x => x.Username == "kammerem"),
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            SinglePlayer = db.Players.First(x => x.Username == "kinzle"),
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            SinglePlayer = db.Players.First(x => x.Username == "EckerS"),
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            SinglePlayer = db.Players.First(x => x.Username == "WimmerG"),
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            SinglePlayer = db.Players.First(x => x.Username == "ReppB"),
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            SinglePlayer = db.Players.First(x => x.Username == "WimmerG"),
            Competition = db.Competitions.First(x => x.Name == "Herren Doppel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            SinglePlayer = db.Players.First(x => x.Username == "ReppB"),
            Competition = db.Competitions.First(x => x.Name == "Herren Doppel")
        });
        db.PlayerCompetitions.Add(new PlayerCompetition()
        {
            SinglePlayer = db.Players.First(x => x.Username == "HoferS"),
            Competition = db.Competitions.First(x => x.Name == "Herren Doppel")
        });
        db.SaveChanges();
        db.Groups.Add(new Group()
        {
            GroupName = "Gruppe A",
            MaxAmount = 4,
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel"),
            GroupPlayers = new List<GroupPlayer>()
            {
                new()
                {
                    Player = db.Players.First(x => x.Username == "asmith")
                },
                new()
                {
                    Player = db.Players.First(x => x.Username == "kammerem")
                },
                new()
                {
                    Player = db.Players.First(x => x.Username == "kinzle")
                },
                new()
                {
                    Player = db.Players.First(x => x.Username == "EckerS")
                }
            }
        });

        db.Groups.Add(new Group
        {
            GroupName = "Gruppe B",
            MaxAmount = 4,
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel"),
            GroupPlayers = new List<GroupPlayer>()
            {
                new()
                {
                    Player = db.Players.First(x => x.Username == "WimmerG")
                },
                new()
                {
                    Player = db.Players.First(x => x.Username == "ReppB")
                }
            }
        });

        // db.Groups.Add(new Group
        // {
        //     GroupName = "Gruppe Doppel A",
        //     MaxAmount = 4,
        //     Competition = db.Competitions.First(x => x.Name == "Herren Doppel"),
        //     GroupPlayers = new List<GroupPlayer>()
        //     {
        //         new()
        //         {
        //             SinglePlayer = db.Players.First(x => x.Username == "kinzle")
        //         },
        //         new()
        //         {
        //             SinglePlayer = db.Players.First(x => x.Username == "EckerS")
        //         }
        //     }
        // });

        db.SaveChanges();
    }
}