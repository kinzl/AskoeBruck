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

    public async Task<IActionResult> OnPostChangeSettingsAsync(string firstname, string lastname, string emailOrPhone, string? nuLigaPlayerUrl)
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser()!;
        CurrentPlayer.Firstname = firstname;
        CurrentPlayer.Lastname = lastname;
        CurrentPlayer.NuLigaPlayerUrl = nuLigaPlayerUrl;

        var user = await userManager.GetUserAsync(User);
        if (user != null && user.Email != emailOrPhone)
        {
            var setUserNameResult = await userManager.SetUserNameAsync(user, emailOrPhone);
            if (!setUserNameResult.Succeeded)
            {
                return RedirectToPage(new { Message = "Fehler: Diese E-Mail ist bereits vergeben oder ungültig." });
            }
            
            var setEmailResult = await userManager.SetEmailAsync(user, emailOrPhone);
            if (!setEmailResult.Succeeded)
            {
                return RedirectToPage(new { Message = "Fehler beim Aktualisieren der E-Mail Adresse." });
            }
            
            await signInManager.RefreshSignInAsync(user);
        }

        await db.SaveChangesAsync();

        return RedirectToPage(nameof(Settings), new { Message = "Daten erfolgreich gespeichert" });
    }

    public async Task<IActionResult> OnPostSaveNotificationSettingsAsync()
    {
        var dbPlayer = currentPlayerService.GetCurrentUser()!;
        if (dbPlayer.NotificationSettings == null)
        {
            dbPlayer.NotificationSettings = new PlayerNotificationSettings();
            db.PlayerNotificationSettings.Add(dbPlayer.NotificationSettings);
        }

        dbPlayer.NotificationSettings.EmailOnOpponentAssigned = CurrentPlayer.NotificationSettings?.EmailOnOpponentAssigned ?? true;
        dbPlayer.NotificationSettings.EmailOnSlotFull = CurrentPlayer.NotificationSettings?.EmailOnSlotFull ?? true;
        dbPlayer.NotificationSettings.EmailOnSlotCancelled = CurrentPlayer.NotificationSettings?.EmailOnSlotCancelled ?? true;
        dbPlayer.NotificationSettings.EmailOnSlotJoined = CurrentPlayer.NotificationSettings?.EmailOnSlotJoined ?? true;
        dbPlayer.NotificationSettings.EmailOnPyramidChallenge = CurrentPlayer.NotificationSettings?.EmailOnPyramidChallenge ?? true;

        await db.SaveChangesAsync();

        return RedirectToPage(nameof(Settings), new { Message = "Benachrichtigungseinstellungen erfolgreich aktualisiert." });
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

    public async Task<IActionResult> OnPostDeleteProfileAsync()
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser()!;
        
        // Soft delete the profile
        CurrentPlayer.IsActive = false;
        await db.SaveChangesAsync();

        // Sign out
        await signInManager.SignOutAsync();

        return RedirectToPage("/Index");
    }

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }
}