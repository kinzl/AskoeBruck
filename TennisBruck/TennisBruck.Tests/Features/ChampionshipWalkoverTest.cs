using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TennisBruck.Features.Reservations;
using TennisDb;
using Xunit;

namespace TennisBruck.Tests.Features;

public class ChampionshipWalkoverTest : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TennisContext _db;

    public ChampionshipWalkoverTest()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TennisContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TennisContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void WalkoverWithPartialScore_SetsAndWinnerAreRecordedProperly()
    {
        // Test retirement at score "4:3 w.o."
        var team1 = new Team { Id = 1 };
        var team2 = new Team { Id = 2 };
        var match = new Match
        {
            Id = 10,
            Team1 = team1,
            Team2 = team2,
            IsWalkover = true,
            WalkoverTeamId = 2, // Team 2 retired
            WinnerTeamId = 1,   // Team 1 wins
            Sets = new List<Set>
            {
                new Set { SetNumber = 1, Player1GamesWon = 4, Player2GamesWon = 3 }
            }
        };

        Assert.True(match.IsWalkover);
        Assert.Equal(2, match.WalkoverTeamId);
        Assert.Equal(1, match.WinnerTeamId);
        Assert.Single(match.Sets);
        Assert.Equal(4, match.Sets[0].Player1GamesWon);
        Assert.Equal(3, match.Sets[0].Player2GamesWon);
    }

    [Fact]
    public void WalkoverWithPartialScore_LeaderRetires_OpponentWins()
    {
        // When Player 1 is ahead 4:3 but retires (e.g. injury), Team 1 is WalkoverTeamId and Team 2 wins
        var team1 = new Team { Id = 1 };
        var team2 = new Team { Id = 2 };
        int walkoverTeamId = team1.Id; // Team 1 gave up
        var winnerTeam = walkoverTeamId == team1.Id ? team2 : team1;

        var match = new Match
        {
            Id = 11,
            Team1 = team1,
            Team2 = team2,
            IsWalkover = true,
            WalkoverTeamId = walkoverTeamId,
            Winner = winnerTeam,
            Sets = new List<Set>
            {
                new Set { SetNumber = 1, Player1GamesWon = 4, Player2GamesWon = 3 }
            }
        };

        Assert.True(match.IsWalkover);
        Assert.Equal(team1.Id, match.WalkoverTeamId);
        Assert.Equal(team2.Id, match.Winner.Id);
        Assert.Equal(4, match.Sets[0].Player1GamesWon);
        Assert.Equal(3, match.Sets[0].Player2GamesWon);
    }

    [Fact]
    public void WalkoverWithoutScore_SetsAreEmptyAndWinnerAssigned()
    {
        // When walkover is given before match starts (no games played, score is empty)
        var team1 = new Team { Id = 1 };
        var team2 = new Team { Id = 2 };
        int walkoverTeamId = team1.Id; // Team 1 gave up
        var winnerTeam = walkoverTeamId == team1.Id ? team2 : team1;

        var match = new Match
        {
            Id = 12,
            Team1 = team1,
            Team2 = team2,
            IsWalkover = true,
            WalkoverTeamId = walkoverTeamId,
            Winner = winnerTeam,
            Sets = new List<Set>()
        };

        Assert.True(match.IsWalkover);
        Assert.Equal(team1.Id, match.WalkoverTeamId);
        Assert.Equal(team2.Id, match.Winner.Id);
        Assert.Empty(match.Sets);
    }

    [Fact]
    public async Task RecurringReservations_BooksConsecutiveWeeksAndDetectsConflicts()
    {
        var service = new ReservationService(_db);

        var player = new Player { Id = 101, Firstname = "Thomas", Lastname = "Muster" };
        var otherPlayer = new Player { Id = 102, Firstname = "Dominic", Lastname = "Thiem" };
        _db.Players.AddRange(player, otherPlayer);

        // Pre-book week 2 on Court 1 to simulate a conflict in the future
        var baseDate = DateTime.Today.AddDays(7).AddHours(14); // Next week Monday 14:00
        var week2Date = baseDate.AddDays(7); // Week 2

        _db.Reservations.Add(new Reservation
        {
            CourtNumber = 1,
            StartTime = week2Date,
            EndTime = week2Date.AddHours(2),
            Player = otherPlayer
        });
        await _db.SaveChangesAsync();

        // Act: player books 3 consecutive weeks
        var (success, message, bookedCount, skippedCount) = service.CreateRecurringReservations(
            courtNumber: 1,
            startDate: baseDate.Date,
            startTime: new TimeSpan(14, 0, 0),
            endTime: new TimeSpan(16, 0, 0),
            currentPlayerId: player.Id,
            partnerId: null,
            eventName: "Training Abo",
            repeatWeeks: 3
        );

        // Assert
        Assert.True(success);
        Assert.Equal(2, bookedCount); // Week 1 and Week 3 booked
        Assert.Equal(1, skippedCount); // Week 2 was conflicting
        Assert.Contains("2 von 3 Terminen gebucht", message);

        var allReservations = await _db.Reservations.Where(r => r.Player.Id == player.Id).ToListAsync();
        Assert.NotEmpty(allReservations);
    }

}
