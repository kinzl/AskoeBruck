using Microsoft.AspNetCore.Identity;

namespace TennisBruck.Pages;

[Authorize]
public class Settings(
    CurrentPlayerService currentPlayerService,
    TennisContext db,
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager)
    : PageModel
{
    public string? InfoText { get; set; }
    [BindProperty] public required Player CurrentPlayer { get; set; }

    public IActionResult OnGet(string? infoText)
    {
        InfoText = infoText;
        CurrentPlayer = currentPlayerService.GetCurrentUser()!;
        return Page();
    }

    public IActionResult OnPostChangeSettings(RegistrationDto body)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser()!;
        CurrentPlayer.Firstname = body.Firstname;
        CurrentPlayer.Lastname = body.Lastname;
        CurrentPlayer.Username = body.Username;
        db.SaveChanges();

        return RedirectToPage(nameof(Settings), new { infoText = "Daten gespeichert" });
    }

    public async Task<IActionResult> OnPostChangePasswordAsync(string oldPassword, string newPassword,
        string newPasswordRepeat)
    {
        // Check if passwords Match each other
        if (newPassword != newPasswordRepeat)
        {
            InfoText = "Die neuen Passwörter stimmen nicht überein!";
            return Page(); // Bleibt auf der Seite und zeigt den Fehler
        }

        //get the Identity-User
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        var changePasswordResult = await userManager.ChangePasswordAsync(user, oldPassword, newPassword);

        if (!changePasswordResult.Succeeded)
        {
            foreach (var error in changePasswordResult.Errors)
            {
                InfoText += error.Description + " ";
            }

            return Page();
        }

        // Success
        await signInManager.RefreshSignInAsync(user);

        InfoText = "Dein Passwort wurde erfolgreich geändert! ✅";
        return Page();
    }

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }
}