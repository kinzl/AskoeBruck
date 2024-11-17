using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TennisBruck.Extensions;
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
        // Query the database for the verification entry
        var verification = _db.RegistrationVerifications
            .FirstOrDefault(x => x.VerificationCode == inputCode && x.ExpiresAt > DateTime.UtcNow);

        if (verification == null)
        {
            return new RedirectToPageResult(nameof(Login), new { errorText = "Ungültiger oder abgelaufener Code." });
        }

        // Check the purpose of the verification entry
        if (verification.Purpose == EnvironmentalVariables.PasswordResetPurpose)
        {
            // Redirect to Reset Password page
            HttpContext.Session.SetString("VerifiedEmailOrPhone", verification.EmailOrPhone);
            return new RedirectToPageResult(nameof(ResetPassword));
        }

        if (verification.Purpose == EnvironmentalVariables.RegistrationPurpose)
        {
            // Complete registration logic
            var entry = _db.RegistrationVerifications.FirstOrDefault(x => x.EmailOrPhone == verification.EmailOrPhone);

            if (entry == null)
            {
                return new RedirectToPageResult(nameof(Verification),
                    new { errorText = "Ungültiger Registrierungsprozess." });
            }

            var user = new Player
            {
                Firstname = entry.Firstname,
                Lastname = entry.Lastname,
                EmailOrPhone = entry.EmailOrPhone,
                Username = entry.Username,
                PasswordHash = entry.PasswordHash,
                IsAdmin = false,
                IsPlayingGrieskirchen = false,
            };

            _db.Players.Add(user);
            _db.RegistrationVerifications.Remove(entry);

            await _db.SaveChangesAsync();

            // Log the user in

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()), // Use NameIdentifier for ID
                new(ClaimTypes.Name, user.Username), // Store username
                new(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User") // Store role (Admin/User)
            };

            // Create claims identity and authentication properties
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true // Set true if you want to persist login across sessions
            };

            // Sign in the user
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity), authProperties);

            return new RedirectToPageResult(nameof(Index));
        }

        return Page();
    }
}