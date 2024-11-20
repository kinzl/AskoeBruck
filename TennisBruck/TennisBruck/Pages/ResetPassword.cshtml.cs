using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TennisBruck.Dto;
using TennisBruck.Extensions;
using TennisDb;

namespace TennisBruck.Pages;

public class ResetPassword : PageModel
{
    private readonly TennisContext _db;
    private readonly PasswordEncryption _pe;

    public ResetPassword(TennisContext db, PasswordEncryption pe)
    {
        _db = db;
        _pe = pe;
    }

    [BindProperty] public ResetPasswordDto Input { get; set; }

    public string ErrorText { get; private set; }

    public IActionResult OnGet(string token)
    {
        // Validate the token
        var user = _db.Players.FirstOrDefault(p => p.PasswordResetToken == token && p.TokenExpiry > DateTime.UtcNow);
        if (user == null)
        {
            return new RedirectToPageResult(nameof(Login), new { message = "Ungültiger oder abgelaufener Token." });
        }

        // Store token in TempData to use during POST
        TempData["ResetToken"] = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Validate model
        if (!ModelState.IsValid)
        {
            ErrorText = "Bitte alle Felder korrekt ausfüllen.";
            return Page();
        }

        // Retrieve token from TempData
        var token = TempData["ResetToken"] as string;
        if (string.IsNullOrEmpty(token))
        {
            return new RedirectToPageResult(nameof(Login),
                new { message = "Session abgelaufen. Bitte erneut versuchen." });
        }

        // Find the user by token
        var user = _db.Players.FirstOrDefault(p => p.PasswordResetToken == token && p.TokenExpiry > DateTime.UtcNow);
        if (user == null)
        {
            return new RedirectToPageResult(nameof(Login), new { message = "Ungültiger oder abgelaufener Token." });
        }

        // Check password confirmation match
        if (Input.NewPassword != Input.ConfirmPassword)
        {
            ErrorText = "Die Passwörter stimmen nicht überein.";
            return Page();
        }

        // Update the password
        user.PasswordHash = _pe.HashPassword(Input.NewPassword);
        user.PasswordResetToken = null; // Invalidate the token
        user.TokenExpiry = null;

        await _db.SaveChangesAsync();

        // Optionally log the password reset event
        Console.WriteLine($"Password reset successfully for user: {user.Id}");

        return new RedirectToPageResult(nameof(Login), new { message = "Passwort wurde erfolgreich zurückgesetzt." });
    }
}