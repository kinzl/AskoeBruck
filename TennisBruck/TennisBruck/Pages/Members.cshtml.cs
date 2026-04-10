using Microsoft.AspNetCore.Identity;

namespace TennisBruck.Pages;

[Authorize]
public class Members(
    TennisContext db,
    ILogger<Members> logger,
    CurrentPlayerService currentPlayerService,
    UserManager<IdentityUser> userManager)
    : PageModel
{
    public required Player LoggedInPlayer { get; set; }
    public required List<Player> AllPlayers { get; set; }
    public string? Message { get; set; }
    public List<string> AdminUserIds { get; set; } = [];

    public async Task<IActionResult> OnGet(string? message)
    {
        Message = message;
        LoggedInPlayer = currentPlayerService.GetCurrentUser()!;
        AllPlayers = db.Players.Include(x => x.IdentityUser).ToList();
        var adminUsers = await userManager.GetUsersInRoleAsync("Admin");
        AdminUserIds = adminUsers.Select(u => u.Id).ToList();
        return Page();
    }

    public IActionResult OnPostCreateUser(RegistrationDto body)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        logger.LogInformation("OnPostCreateUser");
        string password = "askoebruck";
        var player = new Player
        {
            Firstname = body.Firstname,
            Lastname = body.Lastname
        };
        db.Players.Add(player);
        db.SaveChanges();

        return RedirectToPage(new { Message = $"Benutzer wurde erstellt, das Passwort ist {password}" });
    }

    public IActionResult OnPostDeleteUser(int playerId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        logger.LogInformation("OnPostDeleteUser");
        var player = db.Players.Single(x => x.Id == playerId);
        db.Players.Remove(player);
        db.SaveChanges();
        return RedirectToPage(new { Message = $"Benutzer {player.Firstname} {player.Lastname} wurde gelöscht." });
    }

    public async Task<IActionResult> OnPostChangeAdminAsync(int user)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        logger.LogInformation("Toggling admin status for User ID: {User}", user);

        var player = await db.Players
            .Include(p => p.IdentityUser)
            .FirstOrDefaultAsync(p => p.Id == user);

        if (player != null && player.IdentityUser != null)
        {
            bool isAlreadyAdmin = await userManager.IsInRoleAsync(player.IdentityUser, "Admin");

            if (isAlreadyAdmin)
            {
                await userManager.RemoveFromRoleAsync(player.IdentityUser, "Admin");
                logger.LogInformation("Demoted User {User} from Admin.", player.IdentityUser.Email);
                return RedirectToPage(new { Message = $"{player} wurde zum Admin befördert" });
            }

            await userManager.AddToRoleAsync(player.IdentityUser, "Admin");
            logger.LogInformation("Promoted User {User} to Admin.", player.IdentityUser.Email);
            return RedirectToPage(new { Message = $"{player} wurden die Admin berechtigungen entzogen" });
        }

        return RedirectToPage(new { Message = "Ein Fehler ist aufgetreten" });
    }

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }
}