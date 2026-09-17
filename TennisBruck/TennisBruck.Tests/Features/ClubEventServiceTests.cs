using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TennisBruck.Features.Events;
using TennisBruck.Features.Scraping;
using TennisDb;
using Xunit;

namespace TennisBruck.Tests.Features;

public class ClubEventServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TennisContext _db;
    private readonly ClubEventService _service;

    public ClubEventServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TennisContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TennisContext(options);
        _db.Database.EnsureCreated();

        _service = new ClubEventService(_db, null!, NullLogger<ClubEventService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetUpcomingEventsAsync_OrdersPinnedFirstAndExcludesPastEvents()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var pastEvent = new ClubEvent
        {
            Id = 1,
            Title = "Altes Event",
            StartDate = now.AddDays(-10),
            IsPinned = false
        };
        var regularUpcoming = new ClubEvent
        {
            Id = 2,
            Title = "Grillfest",
            StartDate = now.AddDays(5),
            IsPinned = false
        };
        var pinnedUpcoming = new ClubEvent
        {
            Id = 3,
            Title = "Saisonstart (Wichtig)",
            StartDate = now.AddDays(15),
            IsPinned = true
        };

        _db.ClubEvents.AddRange(pastEvent, regularUpcoming, pinnedUpcoming);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.GetUpcomingEventsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(3, result[0].Id); // Pinned is first
        Assert.Equal(2, result[1].Id); // Regular upcoming is second
    }

    [Fact]
    public async Task GetUpcomingTeamMatchesAsync_FiltersByCategoryCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var herrenMatch = new OetvMatch
        {
            Id = 1,
            CompetitionName = "OÖTV 1. Klasse",
            Category = "Herren",
            HomeTeam = "ASKÖ Bruck 1",
            AwayTeam = "UTC Peuerbach 1",
            MatchDateTime = now.AddDays(3),
            Venue = "Tennisanlage ASKÖ Bruck"
        };
        var damenMatch = new OetvMatch
        {
            Id = 2,
            CompetitionName = "OÖTV Bezirksklasse",
            Category = "Damen",
            HomeTeam = "ASKÖ Bruck Damen",
            AwayTeam = "Union Waizenkirchen",
            MatchDateTime = now.AddDays(4),
            Venue = "Tennisanlage ASKÖ Bruck"
        };

        _db.OetvMatches.AddRange(herrenMatch, damenMatch);
        await _db.SaveChangesAsync();

        // Act
        var allMatches = await _service.GetUpcomingTeamMatchesAsync("Alle");
        var herrenOnly = await _service.GetUpcomingTeamMatchesAsync("Herren");
        var damenOnly = await _service.GetUpcomingTeamMatchesAsync("Damen");

        // Assert
        Assert.Equal(2, allMatches.Count);
        Assert.Single(herrenOnly);
        Assert.Equal(1, herrenOnly[0].Id);
        Assert.Single(damenOnly);
        Assert.Equal(2, damenOnly[0].Id);
    }

    [Fact]
    public async Task GetAvailableMatchCategoriesAsync_OnlyReturnsCategoriesWithExistingMatches()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _db.OetvMatches.AddRange(
            new OetvMatch
            {
                Id = 10,
                CompetitionName = "OÖTV 1. Klasse",
                Category = "Herren",
                HomeTeam = "ASKÖ Bruck 1",
                AwayTeam = "UTC Peuerbach 1",
                MatchDateTime = now.AddDays(1)
            },
            new OetvMatch
            {
                Id = 11,
                CompetitionName = "Junior Team",
                Category = "Jugend / Kids",
                HomeTeam = "ASKÖ Bruck Kids",
                AwayTeam = "UTC Ried Kids",
                MatchDateTime = now.AddDays(2)
            }
        );
        await _db.SaveChangesAsync();

        // Act
        var categories = await _service.GetAvailableMatchCategoriesAsync();

        // Assert
        Assert.Equal(2, categories.Count);
        Assert.Contains("Herren", categories);
        Assert.Contains("Jugend / Kids", categories);
        Assert.DoesNotContain("Senioren", categories);
        Assert.DoesNotContain("Mixed", categories);
        Assert.DoesNotContain("Damen", categories);
    }

    [Fact]
    public async Task GetAvailableMatchCategoriesAsync_ExcludesCategoriesWithOnlyPastMatchesWhenUpcomingExist()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _db.OetvMatches.AddRange(
            new OetvMatch
            {
                Id = 20,
                CompetitionName = "OÖTV 1. Klasse",
                Category = "Herren",
                HomeTeam = "ASKÖ Bruck 1",
                AwayTeam = "UTC Peuerbach 1",
                MatchDateTime = now.AddDays(2)
            },
            new OetvMatch
            {
                Id = 21,
                CompetitionName = "Senioren 35",
                Category = "Senioren",
                HomeTeam = "ASKÖ Bruck 35",
                AwayTeam = "UTC Ried 35",
                MatchDateTime = now.AddDays(-10),
                Status = "Beendet"
            }
        );
        await _db.SaveChangesAsync();

        // Act
        var categories = await _service.GetAvailableMatchCategoriesAsync();

        // Assert
        Assert.Contains("Herren", categories);
        Assert.DoesNotContain("Senioren", categories);
    }

    [Fact]
    public void GenerateEventIcs_ProducesValidRfc5545Content()
    {
        // Arrange
        var ev = new ClubEvent
        {
            Id = 42,
            Title = "Schleiferlturnier & Grillabend",
            Description = "Anmeldung bis 18:00 Uhr vor Ort möglich.",
            Location = "Tennisanlage ASKÖ Bruck",
            StartDate = new DateTime(2026, 7, 18, 14, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 18, 22, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var ics = _service.GenerateEventIcs(ev);

        // Assert
        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("SUMMARY:Schleiferlturnier & Grillabend", ics);
        Assert.Contains("LOCATION:Tennisanlage ASKÖ Bruck", ics);
        Assert.Contains("DTSTART:20260718T140000Z", ics);
        Assert.Contains("DTEND:20260718T220000Z", ics);
        Assert.Contains("END:VCALENDAR", ics);
    }

    [Fact]
    public void GenerateMatchIcs_ProducesMatchDetailsWithVenueAndHomeStatus()
    {
        // Arrange
        var match = new OetvMatch
        {
            Id = 10,
            CompetitionName = "OÖTV Regionalklasse West",
            Category = "Herren",
            HomeTeam = "ASKÖ Bruck 1",
            AwayTeam = "UTC Peuerbach 1",
            IsHomeMatch = true,
            MatchDateTime = new DateTime(2026, 5, 23, 13, 0, 0, DateTimeKind.Utc),
            Venue = "Tennisanlage ASKÖ Bruck",
            Notes = "Plätze 1 & 2 reserviert"
        };

        // Act
        var ics = _service.GenerateMatchIcs(match);

        // Assert
        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("SUMMARY:🎾 ASKÖ Bruck 1 vs. UTC Peuerbach 1 (Herren)", ics);
        Assert.Contains("LOCATION:Tennisanlage ASKÖ Bruck", ics);
        Assert.Contains("Heimspiel", ics);
        Assert.Contains("OÖTV Regionalklasse West", ics);
        Assert.Contains("END:VCALENDAR", ics);
    }

    [Fact]
    public async Task UpdateScoreAsync_SavesScoreAndSetsStatusToCompleted()
    {
        // Arrange
        var match = new OetvMatch
        {
            Id = 5,
            CompetitionName = "OÖTV 1. Klasse",
            HomeTeam = "ASKÖ Bruck 1",
            AwayTeam = "UTC Peuerbach 1",
            MatchDateTime = DateTime.UtcNow.AddDays(-1),
            Status = "Geplant"
        };
        _db.OetvMatches.Add(match);
        await _db.SaveChangesAsync();

        // Act
        await _service.UpdateScoreAsync(5, "6 : 3");

        // Assert
        var updated = await _db.OetvMatches.FindAsync(5);
        Assert.NotNull(updated);
        Assert.Equal("6 : 3", updated.Score);
        Assert.Equal("Beendet", updated.Status);
    }

    [Fact]
    public async Task SyncOetvMatchesFromFederationAsync_ImportsAndPersistsClubMatches()
    {
        // Arrange
        var date1 = DateTime.UtcNow.AddDays(5).ToString("dd.MM.yyyy HH:mm");
        var date2 = DateTime.UtcNow.AddDays(10).ToString("dd.MM.yyyy HH:mm");
        var htmlContent = $@"
            <html>
            <body>
                <h2>OÖTV Regionalklasse West</h2>
                <table class='result-set'>
                    <tr><th>Datum</th><th>Heimmannschaft</th><th>Gastmannschaft</th><th>Ergebnis</th></tr>
                    <tr>
                        <td>Sa. {date1}</td>
                        <td>ASKÖ Bruck 1</td>
                        <td>UTC Peuerbach 1</td>
                        <td></td>
                    </tr>
                    <tr>
                        <td>Sa. {date2}</td>
                        <td>ASKÖ Bruck Damen</td>
                        <td>Union Waizenkirchen</td>
                        <td></td>
                    </tr>
                </table>
            </body>
            </html>";

        var httpClient = new HttpClient(new FakeHttpMessageHandler(htmlContent));
        var scraper = new OetvScraperService(httpClient, NullLogger<OetvScraperService>.Instance, null!);
        var service = new ClubEventService(_db, null!, NullLogger<ClubEventService>.Instance, scraper);

        // Act
        var count = await service.SyncOetvMatchesFromFederationAsync("https://www.ooetv.at/liga/vereine/verein/v/40291.html");

        // Assert
        Assert.Equal(2, count);
        var matches = await service.GetUpcomingTeamMatchesAsync("Alle");
        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, m => m.HomeTeam.Contains("Bruck"));
        Assert.Contains(matches, m => m.Category == "Herren");
        Assert.Contains(matches, m => m.Category == "Damen");
    }

    private class FakeHttpMessageHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(content)
            });
        }
    }
}

