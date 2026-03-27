using Microsoft.AspNetCore.Identity;
using Group = TennisDb.Group;

namespace TennisBruck.Services;

public class StartupBackgroundService(IServiceProvider provider) : IHostedService
{
    private readonly IServiceScope _scope = provider.CreateScope();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("ExecuteAsync STARTUP SERVICE");
        var db = _scope.ServiceProvider.GetRequiredService<TennisContext>();
        var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = _scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        // 1. ALTE LOGIK ENTFERNT: Wir reißen nicht mehr bei jedem Start das Haus ab!
        // await DropAllTables(db);
        // await db.Database.EnsureDeletedAsync(cancellationToken);
        // await db.Database.EnsureCreatedAsync(cancellationToken);

        // 2. NEUE LOGIK: Wir wenden ausstehende Updates (Migrationen) sanft an
        Console.WriteLine("Prüfe auf Datenbank-Updates...");
        await db.Database.MigrateAsync(cancellationToken);
        Console.WriteLine("Datenbank ist auf dem neuesten Stand!");

        // 3. SEEDING: Standard-Daten anlegen (falls sie noch nicht existieren)
        // await SeedAdminUserAndPlayer(db, userManager, roleManager);
        // await SeedPlayer(db);
        // SeedCompetition(db);

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAdminUserAndPlayer(TennisContext db, UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        string adminRole = "Admin";
        string myAdminEmail = "kinzl.emil@eclipso.at";

        // 1. Rolle "Admin" anlegen, falls sie nicht existiert
        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(adminRole));
        }

        // 2. Prüfen ob User existiert (ist bei DropAllTables leer, aber sicher ist sicher)
        var adminUser = await userManager.FindByEmailAsync(myAdminEmail);

        if (adminUser == null)
        {
            // 3. IdentityUser SAUBER über den UserManager anlegen
            adminUser = new IdentityUser
            {
                UserName = myAdminEmail,
                Email = myAdminEmail,
                EmailConfirmed = true // E-Mail direkt als bestätigt markieren
            };

            // Hier hasht Microsoft das Passwort sicher im Hintergrund!
            var createResult = await userManager.CreateAsync(adminUser, "AdminPasswort123!");

            if (createResult.Succeeded)
            {
                // 4. Dem frischen User die Admin-Rolle geben
                await userManager.AddToRoleAsync(adminUser, adminRole);

                var player = new Player
                {
                    Firstname = "Emil",
                    Lastname = "Kinzl",
                    Username = "kinzle",
                    IdentityUserId = adminUser.Id
                };

                db.Players.Add(player);
                // Wir speichern den Player direkt, damit er sicher in der DB ist
                await db.SaveChangesAsync();

                Console.WriteLine("ERFOLG: Admin Emil wurde inkl. Rolle komplett angelegt!");
            }
            else
            {
                // Falls das Passwort z.B. zu schwach ist, sehen wir hier warum!
                Console.WriteLine("FEHLER BEIM USER ERSTELLEN:");
                foreach (var error in createResult.Errors)
                {
                    Console.WriteLine($"- {error.Code}: {error.Description}");
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task DropAllTables(TennisContext db)
    {
        var sql = @"
    DO $$
    DECLARE
        r RECORD;
    BEGIN
        FOR r IN (
            SELECT tablename 
            FROM pg_tables 
            WHERE schemaname = 'public'
            AND tablename NOT IN (
                'AspNetUsers',
                'AspNetRoles',
                'AspNetUserRoles',
                'AspNetUserClaims',
                'AspNetRoleClaims',
                'AspNetUserLogins',
                'AspNetUserTokens',
                '__EFMigrationsHistory'
            )
        ) LOOP
            EXECUTE 'DROP TABLE IF EXISTS ""' || r.tablename || '"" CASCADE';
        END LOOP;
    END $$;";

        await db.Database.ExecuteSqlRawAsync(sql);
    }

    private Task SeedPlayer(TennisContext db)
    {
        db.Players.Add(new Player()
        {
            Firstname = "Alice",
            Lastname = "Smith",
            Username = "asmith"
        });

        db.Players.Add(new Player()
        {
            Firstname = "Max",
            Lastname = "Kammerer",
            Username = "kammerem"
        });

        db.Players.Add(new Player()
        {
            Firstname = "Stefan",
            Lastname = "Ecker",
            Username = "EckerS"
        });

        db.Players.Add(new Player()
        {
            Firstname = "Gerald",
            Lastname = "Wimmer",
            Username = "WimmerG"
        });

        db.Players.Add(new Player()
        {
            Firstname = "Bernhard",
            Lastname = "Repp",
            Username = "ReppB"
        });

        db.Players.Add(new Player()
        {
            Firstname = "Stefan",
            Lastname = "Hofer",
            Username = "HoferS"
        });
        db.SaveChanges();
        return Task.CompletedTask;
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
            Team = team
        });
        db.GroupTeams.Add(new GroupTeam
        {
            Group = groupa,
            Team = team1
        });
        db.GroupTeams.Add(new GroupTeam
        {
            Group = groupb,
            Team = team2
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