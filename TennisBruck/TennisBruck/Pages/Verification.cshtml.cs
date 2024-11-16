using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TennisDb;

namespace TennisBruck.Pages;

public class Verification : PageModel
{
    private TennisContext _db;
    public string? ErrorText { get; set; }

    public Verification(TennisContext db)
    {
        _db = db;
    }

    public void OnGet(string? errorText)
    {
        ErrorText = errorText;
    }

    public async Task<IActionResult> OnPostVerifyAsync(string inputCode)
    {
        // Retrieve verification entry
        var entry = _db.RegistrationVerifications
            .FirstOrDefault(x => x.VerificationCode == inputCode && x.ExpiresAt > DateTime.UtcNow);

        if (entry == null)
        {
            return new RedirectToPageResult(nameof(Verification),
                new { errorText = "Ungültiger oder abgelaufener Code" });
        }

        // Complete registration
        var newPlayer = new Player
        {
            Firstname = entry.Firstname,
            Lastname = entry.Lastname,
            EmailOrPhone = entry.EmailOrPhone,
            Username = entry.Username,
            PasswordHash = entry.PasswordHash,
            IsAdmin = false,
            IsPlayingGrieskirchen = false,
        };
        _db.Players.Add(newPlayer);

        // Remove verification entry
        _db.RegistrationVerifications.Remove(entry);

        await _db.SaveChangesAsync();

        return new RedirectToPageResult(nameof(Index));
    }
}