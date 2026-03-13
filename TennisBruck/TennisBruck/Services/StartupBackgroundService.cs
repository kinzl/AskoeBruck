using Group = TennisDb.Group;

namespace TennisBruck.Services;

public class StartupBackgroundService(IServiceProvider provider, PasswordEncryption pe) : IHostedService
{
    private readonly IServiceScope _scope = provider.CreateScope();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("ExecuteAsync STARTUP SERVICE");
        var db = _scope.ServiceProvider.GetRequiredService<TennisContext>();

        // db.Database.EnsureDeleted();
        await DropAllTables(db);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        SeedPlayer(db);
        SeedCompetition(db);

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async Task DropAllTables(TennisContext db)
    {
            var sql = @"
        DO $$
        DECLARE
            r RECORD;
        BEGIN
            FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public') LOOP
                EXECUTE 'DROP TABLE IF EXISTS ""' || r.tablename || '"" CASCADE';
            END LOOP;
        END $$;";
        await db.Database.ExecuteSqlRawAsync(sql);

//         var resetSql = @"
//     DROP SCHEMA public CASCADE;
//     CREATE SCHEMA public;
// ";
//         await db.Database.ExecuteSqlRawAsync(resetSql);
    }

    private void SeedPlayer(TennisContext db)
    {
        db.Players.Add(new Player()
        {
            Firstname = "Alice",
            Lastname = "Smith",
            Username = "asmith",
            IsAdmin = false
        });

        db.Players.Add(new Player()
        {
            Firstname = "Max",
            Lastname = "Kammerer",
            Username = "kammerem",
            IsAdmin = false
        });

        db.Players.Add(new Player()
        {
            Firstname = "Emil",
            Lastname = "Kinzl",
            Username = "kinzle",
            IsAdmin = true
        });

        db.Players.Add(new Player()
        {
            Firstname = "Stefan",
            Lastname = "Ecker",
            Username = "EckerS",
            IsAdmin = true
        });

        db.Players.Add(new Player()
        {
            Firstname = "Gerald",
            Lastname = "Wimmer",
            Username = "WimmerG",
            IsAdmin = true
        });

        db.Players.Add(new Player()
        {
            Firstname = "Bernhard",
            Lastname = "Repp",
            Username = "ReppB",
            IsAdmin = false
        });

        db.Players.Add(new Player()
        {
            Firstname = "Stefan",
            Lastname = "Hofer",
            Username = "HoferS",
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
            RegistrationUntil = DateTime.Now.AddMinutes(1),
            Teams = []
        });
        db.Competitions.Add(new Competition
        {
            Name = "Herren Doppel",
            IsSingle = false,
            RegistrationUntil = DateTime.Now.AddMinutes(1),
            Teams = []
        });
        db.SaveChanges();


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

        var tournamentRegistration4 = new TournamentRegistration()
        {
            CompetitionId = 2,
            PlayerId = 1,
            RegisteredAt = DateTime.Now
        };

        var tournamentRegistration5 = new TournamentRegistration()
        {
            CompetitionId = 2,
            PlayerId = 2,
            RegisteredAt = DateTime.Now
        };

        var tournamentRegistration6 = new TournamentRegistration()
        {
            CompetitionId = 2,
            PlayerId = 4,
            RegisteredAt = DateTime.Now
        };

        db.TournamentRegistrations.Add(tournamentRegistration1);
        db.TournamentRegistrations.Add(tournamentRegistration2);
        db.TournamentRegistrations.Add(tournamentRegistration3);
        db.TournamentRegistrations.Add(tournamentRegistration4);
        db.TournamentRegistrations.Add(tournamentRegistration5);
        db.TournamentRegistrations.Add(tournamentRegistration6);
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
            Competition = db.Competitions.First(x => x.Name == "Herren Einzel"),
        };
        db.Teams.Add(team2);
        // var team3 = new Team
        // {
        //     Competition = db.Competitions.First(x => x.Name == "Herren Doppel"),
        // };
        // db.Teams.Add(team3);
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

        var groupc = new Group
        {
            GroupName = "Gruppe A",
            MaxAmount = 4,
            Competition = db.Competitions.First(x => x.Name == "Herren Doppel")
        };
        db.Groups.Add(groupc);
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
        // db.GroupTeams.Add(new GroupTeam
        // {
        //     Group = groupc,
        //     Team = team3,
        //     Points = 0
        // });
        db.SaveChanges();
    }
}