using TennisDb;

namespace TennisBruck.Services;

public class PlanService(TennisContext db)
{
    public void GeneratePlanGrieskirchen(DateTime startDate, DateTime endDate)
    {
        // Clear existing data
        db.Court.RemoveRange(db.Court);
        db.PlayerCourtGrieskirchen.RemoveRange(db.PlayerCourtGrieskirchen);

        // Retrieve players who are marked as playing in Grieskirchen
        var players = db.Players.Where(x => x.IsPlayingGrieskirchen).ToList();
        if (players.Count < 4) return;

        // Generate match days for every Friday within the date range
        var matchDays = new List<DateTime>();
        for (var date = startDate; date <= endDate; date = date.AddDays(7))
        {
            matchDays.Add(date);
        }

        // Dictionary to track the number of matches each player has participated in
        var playerMatchCounts = players.ToDictionary(player => player, player => 0);

        var random = new Random();

        foreach (var matchDay in matchDays)
        {
            // Randomly shuffle players at each iteration for diversity
            players = players.OrderBy(x => random.Next()).ToList();

            // Select the four players with the fewest matches so far
            var selectedPlayers = players
                .OrderBy(p => playerMatchCounts[p])
                .Take(4)
                .ToList();

            // Create a new Court entry for each match day
            var court = new Court
            {
                MatchDay = matchDay,
                PlayerCourtGrieskirchens = new List<PlayerCourtGrieskirchen>()
            };

            // Add each selected player to the court
            foreach (var player in selectedPlayers)
            {
                court.PlayerCourtGrieskirchens.Add(new PlayerCourtGrieskirchen
                {
                    Player = player,
                    Court = court
                });

                // Increment the match count for each selected player
                playerMatchCounts[player]++;
            }

            // Add the court entry with players to the database
            db.Court.Add(court);
        }

        db.SaveChanges();
        Console.WriteLine("Balanced plan generated successfully.");
    }
}