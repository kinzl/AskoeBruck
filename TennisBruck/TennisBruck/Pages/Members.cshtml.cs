namespace TennisBruck.Pages;

[Authorize]
public class Members(
    TennisContext db,
    ILogger<Members> logger,
    CurrentPlayerService currentPlayerService)
    : PageModel
{
    public required Player LoggedInPlayer { get; set; }
    public required List<Player> AllPlayers { get; set; }
    public string? InfoBox { get; set; }

    public RedirectToPageResult? OnGet(string? infoBox)
    {
        InfoBox = infoBox;
        LoggedInPlayer = currentPlayerService.GetCurrentUser()!;
        AllPlayers = db.Players.ToList();
        return null;
    }

    public IActionResult OnPostCreateUser(RegistrationDto body)
    {
        logger.LogInformation("OnPostCreateUser");
        string password = "askoebruck";
        var player = new Player
        {
            Firstname = body.Firstname,
            Lastname = body.Lastname,
            Username = body.Username,
            IsAdmin = false
        };
        db.Players.Add(player);
        db.SaveChanges();

        return new RedirectToPageResult(nameof(Members),
            new { infoBox = $"Benutzer wurde erstellt, das Passwort ist {password}" });
    }

    public IActionResult OnPostDeleteUser(int playerId)
    {
        logger.LogInformation("OnPostDeleteUser");
        var player = db.Players.Single(x => x.Id == playerId);
        db.Players.Remove(player);
        db.SaveChanges();
        return RedirectToPage(nameof(Members),
            new { InfoBox = $"Benutzer {player.Firstname} {player.Lastname} wurde gelöscht." });
    }

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }

    public IActionResult OnPostChangeAdmin(int user)
    {
        logger.LogInformation("Toggling admin status for User ID: {User}", user);

        var player = db.Players.FirstOrDefault(p => p.Id == user);
        if (player != null)
        {
            player.IsAdmin = !player.IsAdmin; // Toggle admin status
            db.SaveChanges();
        }

        return RedirectToPage();
    }
}