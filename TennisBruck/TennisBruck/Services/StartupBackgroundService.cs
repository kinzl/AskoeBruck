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
            Teams = []
        });
        db.Competitions.Add(new Competition
        {
            Name = "Herren Doppel",
            IsSingle = false,
            Teams = []
        });
        db.SaveChanges();
        // db.PlayerCompetitions.Add(new PlayerCompetition()
        // {
        //     Registered = db.Players.First(x => x.Username == "asmith"),
        //     SinglePlayer = db.Players.First(x => x.Username == "asmith"),
        //     Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        // });
        // db.PlayerCompetitions.Add(new PlayerCompetition()
        // {
        //     Registered = db.Players.First(x => x.Username == "kammerem"),
        //     SinglePlayer = db.Players.First(x => x.Username == "kammerem"),
        //     Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        // });
        // db.PlayerCompetitions.Add(new PlayerCompetition()
        // {
        //     Registered = db.Players.First(x => x.Username == "kinzle"),
        //     SinglePlayer = db.Players.First(x => x.Username == "kinzle"),
        //     Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        // });
        // db.PlayerCompetitions.Add(new PlayerCompetition()
        // {
        //     Registered = db.Players.First(x => x.Username == "EckerS"),
        //     SinglePlayer = db.Players.First(x => x.Username == "EckerS"),
        //     Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        // });
        // db.PlayerCompetitions.Add(new PlayerCompetition()
        // {
        //     Registered = db.Players.First(x => x.Username == "WimmerG"),
        //     SinglePlayer = db.Players.First(x => x.Username == "WimmerG"),
        //     Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        // });
        // db.PlayerCompetitions.Add(new PlayerCompetition()
        // {
        //     Registered = db.Players.First(x => x.Username == "ReppB"),
        //     SinglePlayer = db.Players.First(x => x.Username == "ReppB"),
        //     Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        // });
        // db.PlayerCompetitions.Add(new PlayerCompetition()
        // {
        //     Registered = db.Players.First(x => x.Username == "WimmerG"),
        //     // SinglePlayer = db.Players.First(x => x.Username == "WimmerG"),
        //     Competition = db.Competitions.First(x => x.Name == "Herren Doppel")
        // });
        // db.PlayerCompetitions.Add(new PlayerCompetition()
        // {
        //     Registered = db.Players.First(x => x.Username == "ReppB"),
        //     // SinglePlayer = db.Players.First(x => x.Username == "ReppB"),
        //     Competition = db.Competitions.First(x => x.Name == "Herren Doppel")
        // });
        // db.PlayerCompetitions.Add(new PlayerCompetition()
        // {
        //     Registered = db.Players.First(x => x.Username == "HoferS"),
        //     Competition = db.Competitions.First(x => x.Name == "Herren Doppel")
        // });
        // db.SaveChanges();

        var tournamentRegistration1 = new TournamentRegistration()
        {
            CompetitionId = 1,
            PlayerId = 1,
            RegisteredAt = DateTime.Now
        };

        var tournamentRegistration2 = new TournamentRegistration()
        {
            CompetitionId = 1,
            PlayerId = 2,
            RegisteredAt = DateTime.Now
        };

        var tournamentRegistration3 = new TournamentRegistration()
        {
            CompetitionId = 1,
            PlayerId = 3,
            RegisteredAt = DateTime.Now
        };

        db.TournamentRegistrations.Add(tournamentRegistration1);
        db.TournamentRegistrations.Add(tournamentRegistration2);
        db.TournamentRegistrations.Add(tournamentRegistration3);
        db.SaveChanges();
        // 1 Team erstellen und Competition zuweisen
        var team = new Team
        {
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        };
        db.Teams.Add(team);
        var team1 = new Team
        {
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        };
        db.Teams.Add(team1);
        var team2 = new Team
        {
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        };
        db.Teams.Add(team2);
        db.SaveChanges(); // TeamId wird benötigt für TeamPlayer FK

// Spieler zu Team zuweisen
        var player1 = db.Players.First(x => x.Username == "asmith");
        var player2 = db.Players.First(x => x.Username == "kinzle");
        var player3 = db.Players.First(x => x.Username == "kammerem");

        db.TeamPlayer.AddRange(new[]
        {
            new TeamPlayer { Player = player1, Team = team },
            new TeamPlayer { Player = player2, Team = team1 },
            new TeamPlayer { Player = player3, Team = team2 },
        });
        db.SaveChanges();

// Team der Gruppe hinzufügen
        var groupa = new Group
        {
            GroupName = "Gruppe A",
            MaxAmount = 4,
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        };
        db.Groups.Add(groupa);

        var groupb = new Group
        {
            GroupName = "Gruppe B",
            MaxAmount = 4,
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel")
        };
        db.Groups.Add(groupb);
        db.SaveChanges(); // GroupId wird benötigt

//  GroupTeam erstellen
        db.GroupTeams.Add(new GroupTeam
        {
            Group = groupa,
            Team = team,
            Points = 0
        });
        db.GroupTeams.Add(new GroupTeam
        {
            Group = groupa,
            Team = team1,
            Points = 0
        });
        db.GroupTeams.Add(new GroupTeam
        {
            Group = groupb,
            Team = team2,
            Points = 0
        });
        db.SaveChanges();
    }
}