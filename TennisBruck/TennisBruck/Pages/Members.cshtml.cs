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

    public IActionResult OnPostDeleteUser(int playerId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        logger.LogInformation("OnPostDeleteUser");
        var player = db.Players.Single(x => x.Id == playerId);

        // Soft-Delete anstatt echtem Löschen
        player.IsActive = false;

        db.SaveChanges();
        return RedirectToPage(new { Message = $"Benutzer {player.Firstname} {player.Lastname} wurde deaktiviert." });
    }

    public async Task<IActionResult> OnPostChangeAdminAsync(int user)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        logger.LogInformation("Toggling admin status for User ID: {User}", user);

        var player = await db.Players
            .Include(p => p.IdentityUser)
            .FirstOrDefaultAsync(p => p.Id == user);

        if (player?.IdentityUser == null)
            return RedirectToPage(new { Message = "Ein Fehler ist aufgetreten" });
        bool isAlreadyAdmin = await userManager.IsInRoleAsync(player.IdentityUser, "Admin");

        if (isAlreadyAdmin)
        {
            await userManager.RemoveFromRoleAsync(player.IdentityUser, "Admin");
            await userManager.UpdateSecurityStampAsync(player.IdentityUser);
            logger.LogInformation("Demoted User {User} from Admin", player.IdentityUser.Email);
            return RedirectToPage(new { Message = $"{player} wurden die Admin berechtigungen entzogen" });
        }

        await userManager.AddToRoleAsync(player.IdentityUser, "Admin");
        await userManager.UpdateSecurityStampAsync(player.IdentityUser);
        logger.LogInformation("Promoted User {User} to Admin", player.IdentityUser.Email);
        return RedirectToPage(new { Message = $"{player} wurde zum Admin befördert" });
    }

    /// <summary>
    /// Admin creates a new player — email is optional.
    /// Without email: offline/dummy player (no login account).
    /// With email: a real IdentityUser is created with a random password.
    /// </summary>
    public async Task<IActionResult> OnPostCreateMemberAsync(
        string firstname, string lastname, string? email, decimal? itn)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        if (string.IsNullOrWhiteSpace(firstname) || string.IsNullOrWhiteSpace(lastname))
            return RedirectToPage(new { Message = "Vor- und Nachname sind Pflichtfelder." });

        var player = new Player
        {
            Firstname = firstname.Trim(),
            Lastname = lastname.Trim(),
            Itn = itn,
            IsActive = true,
            HallEntities = [],
            TournamentRegistrations = [],
            NotificationSettings = new PlayerNotificationSettings()
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            var trimmedEmail = email.Trim();

            // Check if email is already taken
            var existing = await userManager.FindByEmailAsync(trimmedEmail);
            if (existing != null)
                return RedirectToPage(new { Message = $"Die E-Mail '{trimmedEmail}' ist bereits vergeben." });

            var identityUser = new IdentityUser
            {
                UserName = trimmedEmail,
                Email = trimmedEmail,
                EmailConfirmed = true
            };

            // Random 16-char password — admin can send a password-reset link later
            var password = GenerateRandomPassword();
            var result = await userManager.CreateAsync(identityUser, password);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToPage(new { Message = $"Fehler: {errors}" });
            }

            player.IdentityUser = identityUser;
            player.IdentityUserId = identityUser.Id;
        }

        db.Players.Add(player);
        await db.SaveChangesAsync();

        var suffix = player.IdentityUser == null
            ? "(Offline-Mitglied, kein Login)"
            : "(mit Login-Account)";
        return RedirectToPage(new
        {
            Message = $"{player.Firstname} {player.Lastname} wurde erfolgreich erstellt. {suffix}"
        });
    }

    /// <summary>
    /// Admin edits an existing player's name, ITN and email.
    /// For offline players (no IdentityUser): providing an email creates a new login account.
    /// For players with an account: providing a changed email updates it.
    /// </summary>
    public async Task<IActionResult> OnPostEditPlayerAsync(
        int editPlayerId, string editFirstname, string editLastname, decimal? editItn, string? editEmail)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        if (string.IsNullOrWhiteSpace(editFirstname) || string.IsNullOrWhiteSpace(editLastname))
            return RedirectToPage(new { Message = "Vor- und Nachname sind Pflichtfelder." });

        var player = await db.Players
            .Include(p => p.IdentityUser)
            .FirstOrDefaultAsync(p => p.Id == editPlayerId);

        if (player == null) return NotFound();

        player.Firstname = editFirstname.Trim();
        player.Lastname = editLastname.Trim();

        // Only allow manual ITN edits when there is no ÖTV/NuLiga sync URL
        if (string.IsNullOrEmpty(player.NuLigaPlayerUrl))
            player.Itn = editItn;

        if (!string.IsNullOrWhiteSpace(editEmail))
        {
            var trimmedEmail = editEmail.Trim();

            if (player.IdentityUser == null)
            {
                // Offline player → create a new IdentityUser and link it
                var existing = await userManager.FindByEmailAsync(trimmedEmail);
                if (existing != null)
                    return RedirectToPage(new { Message = $"Die E-Mail '{trimmedEmail}' ist bereits vergeben." });

                var identityUser = new IdentityUser
                {
                    UserName = trimmedEmail,
                    Email = trimmedEmail,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(identityUser, GenerateRandomPassword());
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return RedirectToPage(new { Message = $"Fehler beim Erstellen des Accounts: {errors}" });
                }

                player.IdentityUser = identityUser;
                player.IdentityUserId = identityUser.Id;

                await db.SaveChangesAsync();
                return RedirectToPage(new
                {
                    Message = $"{player.Firstname} {player.Lastname} wurde aktualisiert und erhält nun einen Login-Account."
                });
            }
            else
            {
                // Existing account → update email if it changed
                if (!string.Equals(player.IdentityUser.Email, trimmedEmail, StringComparison.OrdinalIgnoreCase))
                {
                    var conflict = await userManager.FindByEmailAsync(trimmedEmail);
                    if (conflict != null && conflict.Id != player.IdentityUser.Id)
                        return RedirectToPage(new { Message = $"Die E-Mail '{trimmedEmail}' ist bereits vergeben." });

                    player.IdentityUser.Email = trimmedEmail;
                    player.IdentityUser.UserName = trimmedEmail;
                    player.IdentityUser.NormalizedEmail = trimmedEmail.ToUpperInvariant();
                    player.IdentityUser.NormalizedUserName = trimmedEmail.ToUpperInvariant();
                    await userManager.UpdateAsync(player.IdentityUser);
                }
            }
        }

        await db.SaveChangesAsync();
        return RedirectToPage(new { Message = $"{player.Firstname} {player.Lastname} wurde aktualisiert." });
    }

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GenerateRandomPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$";
        var rng = new Random();
        return new string(Enumerable.Range(0, 16).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }
}