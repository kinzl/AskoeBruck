using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using TennisDb;
using Match = TennisDb.Match;

namespace TennisBruck.Features.Championship;

public class TournamentBracketEngine
{
    private static readonly List<int> KnownBrackets = [2, 4, 8, 16, 32];

    public void CreateOrUpdateBracket(TennisContext db, int competitionId, int size, string phaseName)
    {
        if (competitionId == 0) return;

        var matchIds = db.KnockoutMatch
            .Where(k => k.CompetitionId == competitionId && k.PhaseName == phaseName)
            .Select(k => k.Id)
            .ToList();

        if (matchIds.Any())
        {
            db.Sets.Where(s => matchIds.Contains(s.Match.Id)).ExecuteDelete();
        }

        db.KnockoutMatch.Where(k => k.CompetitionId == competitionId && k.PhaseName == phaseName)
            .ExecuteDelete();
        db.SaveChanges();

        int closest = KnownBrackets.First(k => k >= size);
        int byes = closest - size;
        if (byes > 0) size = closest;

        int round = 1;
        double baseT = (double)size / 2;
        double baseC = (double)size / 2;
        int matchId = 1;
        int nextInc = size / 2;

        for (int i = 1; i <= (size - 1); i++)
        {
            double baseR = i / baseT;
            bool isBye = byes > 0 && (i % 2 != 0 || byes >= (baseT - i));

            if (isBye) byes--;

            db.KnockoutMatch.Add(new KnockoutMatch
            {
                CompetitionId = competitionId,
                PhaseName = phaseName,
                BracketNo = matchId++,
                RoundNo = round,
                IsBye = isBye,
                NextGame = nextInc + i > size - 1 ? null : nextInc + i
            });

            if (i % 2 != 0) nextInc--;

            while (baseR >= 1)
            {
                round++;
                baseC /= 2;
                baseT += baseC;
                baseR = i / baseT;
            }
        }

        db.SaveChanges();
    }

    public void AdvanceWinnerInBracket(TennisContext db, Match match, IEmailSender emailSender)
    {
        if (match is not KnockoutMatch km || km.NextGame == null || match.Winner == null)
            return;

        var nextMatch = db.KnockoutMatch
            .Include(m => m.Team1)
            .Include(m => m.Team2)
            .FirstOrDefault(m => m.BracketNo == km.NextGame
                                 && m.CompetitionId == km.CompetitionId
                                 && m.PhaseName == km.PhaseName);

        if (nextMatch == null) return;

        if (nextMatch.Team1 == null)
            nextMatch.Team1 = match.Winner;
        else if (nextMatch.Team2 == null)
            nextMatch.Team2 = match.Winner;

        db.SaveChanges();
        NotifyOpponentsIfAssigned(db, nextMatch, emailSender);
    }

    public void UndoWinnerAdvancement(TennisContext db, KnockoutMatch km, Team previousWinner)
    {
        if (km.NextGame == null) return;

        var nextMatch = db.KnockoutMatch
            .Include(m => m.Team1)
            .Include(m => m.Team2)
            .FirstOrDefault(m => m.BracketNo == km.NextGame
                                 && m.CompetitionId == km.CompetitionId
                                 && m.PhaseName == km.PhaseName);

        if (nextMatch == null) return;

        if (nextMatch.Team1?.Id == previousWinner.Id)
            nextMatch.Team1 = null;
        else if (nextMatch.Team2?.Id == previousWinner.Id)
            nextMatch.Team2 = null;

        db.SaveChanges();
    }

    public void NotifyOpponentsIfAssigned(TennisContext db, KnockoutMatch nextMatch, IEmailSender emailSender)
    {
        if (nextMatch.Team1 == null || nextMatch.Team2 == null) return;

        var team1 = db.Teams
            .Include(t => t.TeamPlayers).ThenInclude(tp => tp.Player).ThenInclude(p => p.NotificationSettings)
            .Include(t => t.TeamPlayers).ThenInclude(tp => tp.Player).ThenInclude(p => p.IdentityUser)
            .FirstOrDefault(t => t.Id == nextMatch.Team1.Id);

        var team2 = db.Teams
            .Include(t => t.TeamPlayers).ThenInclude(tp => tp.Player).ThenInclude(p => p.NotificationSettings)
            .Include(t => t.TeamPlayers).ThenInclude(tp => tp.Player).ThenInclude(p => p.IdentityUser)
            .FirstOrDefault(t => t.Id == nextMatch.Team2.Id);

        if (team1 == null || team2 == null) return;

        var team1Players = team1.TeamPlayers.Select(tp => tp.Player).ToList();
        var team2Players = team2.TeamPlayers.Select(tp => tp.Player).ToList();

        var team1Names = string.Join(" / ", team1Players.Select(p => $"{p.Firstname} {p.Lastname}"));
        var team2Names = string.Join(" / ", team2Players.Select(p => $"{p.Firstname} {p.Lastname}"));

        var notifications = new[]
        {
            (players: team1Players, myNames: team1Names, oppNames: team2Names),
            (players: team2Players, myNames: team2Names, oppNames: team1Names)
        };

        foreach (var (players, myNames, oppNames) in notifications)
        {
            foreach (var p in players)
            {
                if (p.IdentityUser?.Email != null &&
                    (p.NotificationSettings == null || p.NotificationSettings.EmailOnOpponentAssigned))
                {
                    var subject = "🎾 Dein Gegner im K.O.-Raster steht fest!";
                    var body = $"Hallo {p.Firstname},<br><br>" +
                               $"dein nächster Gegner im K.O.-Raster steht fest! Du ({myNames}) spielst gegen <strong>{oppNames}</strong>.<br><br>" +
                               $"Viel Erfolg beim Match!<br>Dein TennisBruck-Team";
                    _ = emailSender.SendEmailAsync(p.IdentityUser.Email, subject, body);
                }
            }
        }
    }
}
