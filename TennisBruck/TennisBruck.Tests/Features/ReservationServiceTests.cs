using Microsoft.EntityFrameworkCore;
using TennisBruck.Features.Reservations;
using TennisDb;
using Xunit;

namespace TennisBruck.Tests.Features;

public class ReservationServiceTests
{
    [Fact]
    public void CalculateBlockInfo_SingleReservation_HasRowSpanOneAndIsStart()
    {
        // Arrange
        var service = new ReservationService(null!);
        var date = new DateTime(2026, 6, 1, 10, 0, 0);
        var res = new Reservation
        {
            Id = 1,
            CourtNumber = 1,
            StartTime = date,
            EndTime = date.AddMinutes(30),
            Player = new Player { Id = 10, Firstname = "Max", Lastname = "Mustermann" }
        };

        // Act
        var blockInfo = service.CalculateBlockInfo(new List<Reservation> { res });

        // Assert
        Assert.Single(blockInfo);
        Assert.True(blockInfo.ContainsKey((1, date)));
        var block = blockInfo[(1, date)];
        Assert.Equal(1, block.RowSpan);
        Assert.True(block.IsStart);
        Assert.Equal(1, block.Reservation.Id);
    }

    [Fact]
    public void CalculateBlockInfo_ContiguousSlotsSamePlayer_MergesIntoSingleBlockWithCorrectRowSpan()
    {
        // Arrange
        var service = new ReservationService(null!);
        var t1 = new DateTime(2026, 6, 1, 10, 0, 0);
        var t2 = t1.AddMinutes(30);
        var t3 = t2.AddMinutes(30);

        var player = new Player { Id = 10, Firstname = "Max", Lastname = "Mustermann" };
        var partner = new Player { Id = 20, Firstname = "Anna", Lastname = "Musterfrau" };

        var res1 = new Reservation
        {
            Id = 1,
            CourtNumber = 1,
            StartTime = t1,
            EndTime = t2,
            Player = player,
            PartnerId = partner.Id
        };
        var res2 = new Reservation
        {
            Id = 2,
            CourtNumber = 1,
            StartTime = t2,
            EndTime = t3,
            Player = player,
            PartnerId = partner.Id
        };

        // Act
        var blockInfo = service.CalculateBlockInfo(new List<Reservation> { res1, res2 });

        // Assert
        Assert.Equal(2, blockInfo.Count);

        // Start block has RowSpan = 2, IsStart = true
        var startBlock = blockInfo[(1, t1)];
        Assert.Equal(2, startBlock.RowSpan);
        Assert.True(startBlock.IsStart);

        // Continuation block has IsStart = false
        var continuationBlock = blockInfo[(1, t2)];
        Assert.False(continuationBlock.IsStart);
        Assert.Equal(2, continuationBlock.Reservation.Id);
    }

    [Fact]
    public void CalculateBlockInfo_DifferentPlayersContiguousSlots_AreNotMerged()
    {
        // Arrange
        var service = new ReservationService(null!);
        var t1 = new DateTime(2026, 6, 1, 10, 0, 0);
        var t2 = t1.AddMinutes(30);

        var player1 = new Player { Id = 10, Firstname = "Max", Lastname = "M" };
        var player2 = new Player { Id = 20, Firstname = "Anna", Lastname = "A" };

        var res1 = new Reservation
        {
            Id = 1,
            CourtNumber = 1,
            StartTime = t1,
            EndTime = t2,
            Player = player1
        };
        var res2 = new Reservation
        {
            Id = 2,
            CourtNumber = 1,
            StartTime = t2,
            EndTime = t2.AddMinutes(30),
            Player = player2
        };

        // Act
        var blockInfo = service.CalculateBlockInfo(new List<Reservation> { res1, res2 });

        // Assert
        Assert.Equal(2, blockInfo.Count);
        Assert.True(blockInfo[(1, t1)].IsStart);
        Assert.Equal(1, blockInfo[(1, t1)].RowSpan);

        Assert.True(blockInfo[(1, t2)].IsStart);
        Assert.Equal(1, blockInfo[(1, t2)].RowSpan);
    }

    [Fact]
    public void CreateRecurringReservations_ClampsToMax20Weeks()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<TennisContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new TennisContext(options);
        db.Database.EnsureCreated();

        var player = new Player { Id = 1, Firstname = "Max", Lastname = "Mustermann" };
        db.Players.Add(player);
        db.SaveChanges();

        var service = new ReservationService(db);
        var futureDate = DateTime.Today.AddDays(7);

        // Request 30 weeks - should be clamped to 20
        var result = service.CreateRecurringReservations(
            courtNumber: 1,
            startDate: futureDate,
            startTime: new TimeSpan(10, 0, 0),
            endTime: new TimeSpan(11, 0, 0),
            currentPlayerId: 1,
            partnerId: null,
            eventName: "Training",
            repeatWeeks: 30);

        Assert.True(result.Success);
        Assert.Equal(20, result.BookedCount);
        // Each week has two 30-min slots: 10:00-10:30 and 10:30-11:00 => 20 * 2 = 40 reservations
        Assert.Equal(40, db.Reservations.Count());
    }

    [Fact]
    public void CreateRecurringReservations_CustomWeeks_CreatesExactCount()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<TennisContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new TennisContext(options);
        db.Database.EnsureCreated();

        var player = new Player { Id = 1, Firstname = "Max", Lastname = "Mustermann" };
        db.Players.Add(player);
        db.SaveChanges();

        var service = new ReservationService(db);
        var futureDate = DateTime.Today.AddDays(7);

        var result = service.CreateRecurringReservations(
            courtNumber: 2,
            startDate: futureDate,
            startTime: new TimeSpan(14, 0, 0),
            endTime: new TimeSpan(15, 0, 0),
            currentPlayerId: 1,
            partnerId: null,
            eventName: "Serientermin",
            repeatWeeks: 7);

        Assert.True(result.Success);
        Assert.Equal(7, result.BookedCount);
        Assert.Equal(14, db.Reservations.Count());
    }
}

