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
    public string? Message { get; set; }
    [BindProperty] public required Player CurrentPlayer { get; set; }

    public IActionResult OnGet(string? message)
    {
        Message = message;
        CurrentPlayer = currentPlayerService.GetCurrentUser()!;
        return Page();
    }

    public IActionResult OnPostChangeSettings(RegistrationDto body)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser()!;
        CurrentPlayer.Firstname = body.Firstname;
        CurrentPlayer.Lastname = body.Lastname;
        db.SaveChanges();

        return RedirectToPage(nameof(Settings), new { Message = "Daten gespeichert" });
    }

    public async Task<IActionResult> OnPostChangePasswordAsync(string oldPassword, string newPassword,
        string newPasswordRepeat)
    {
        if (newPassword != newPasswordRepeat)
            return RedirectToPage(new { Message = "Die neuen Passwörter stimmen nicht überein!" });

        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");

        var changePasswordResult = await userManager.ChangePasswordAsync(user, oldPassword, newPassword);

        if (!changePasswordResult.Succeeded)
        {
            foreach (var error in changePasswordResult.Errors)
            {
                Message += error.Description + " ";
            }

            return RedirectToPage(new { Message });
        }

        await signInManager.RefreshSignInAsync(user);

        return RedirectToPage(new { Message = "Dein Passwort wurde erfolgreich geändert!" });
    }

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }
}