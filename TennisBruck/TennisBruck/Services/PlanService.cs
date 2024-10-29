using TennisDb;

namespace TennisBruck.Services;

public class PlanService
{
    private TennisContext _db;

    public PlanService(TennisContext db)
    {
        _db = db;
    }

    public void GeneratePlanGrieskirchen(DateTime startDate, DateTime endDate)
    {
        // Remove all existing courts
        _db.Court.RemoveRange(_db.Court);
        _db.PlayerCourtGrieskirchen.RemoveRange(_db.PlayerCourtGrieskirchen);
        // Retrieve players who are marked as playing in Grieskirchen
        var players = _db.Players.Where(x => x.IsPlayingGrieskirchen).ToList();
        if (players.Count < 4) return;

        // DateTime startDate = new DateTime(2024, 10, 11);
        // DateTime endDate = new DateTime(2025, 4, 18);

        // Initialize match days (every Friday within the range)
        var matchDays = new List<DateTime>();
        for (var date = startDate; date <= endDate; date = date.AddDays(7))
        {
            if (date.DayOfWeek == DayOfWeek.Friday)
            {
                matchDays.Add(date);
            }
        }

        // Number of matches each player should ideally play
        int matchesPerPlayer = matchDays.Count * 4 / players.Count;
        int extraMatches = (matchDays.Count * 4) % players.Count; // Handle uneven distribution

        // Track the count of matches each player has been assigned
        var playerMatches = players.ToDictionary(p => p, p => 0);

        int playerIndex = 0;
        foreach (var matchDay in matchDays)
        {
            var currentPlayers = new List<Player>();

            // Select 4 players for this match day
            while (currentPlayers.Count < 4)
            {
                var player = players[playerIndex];

                // Check if player has reached their target number of matches
                if (playerMatches[player] < matchesPerPlayer ||
                    (extraMatches > 0 && playerMatches[player] == matchesPerPlayer))
                {
                    currentPlayers.Add(player);
                    playerMatches[player]++;
                    if (playerMatches[player] == matchesPerPlayer + 1) extraMatches--;
                }

                playerIndex = (playerIndex + 1) % players.Count;
            }

            // Create a new Court entry for each match day
            var court = new Court
            {
                MatchDay = matchDay,
                PlayerCourtGrieskirchens = new List<PlayerCourtGrieskirchen>()
            };

            // Add each player to Courts for this court
            foreach (var player in currentPlayers)
            {
                court.PlayerCourtGrieskirchens.Add(new PlayerCourtGrieskirchen
                {
                    Player = player,
                    Court = court
                });
            }

            // Add the court entry with players to the database
            _db.Court.Add(court);
        }

        _db.SaveChanges();
        Console.WriteLine("Plan generated successfully.");
    }
}