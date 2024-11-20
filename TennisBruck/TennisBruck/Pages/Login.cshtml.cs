using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TennisBruck.Extensions;
using TennisBruck.Services;
using TennisBruck.wwwroot.Dto;
using TennisDb;

namespace TennisBruck.Pages;

public class Login : PageModel
{
    public string? Message { get; set; }

    private TennisContext _db;
    private PasswordEncryption _pe;
    private readonly ILogger<IndexModel> _logger;
    private readonly EmailService _emailService;

    public Login(TennisContext db, PasswordEncryption pe, ILogger<IndexModel> logger, EmailService emailService)
    {
        _db = db;
        _pe = pe;
        _logger = logger;
        _emailService = emailService;
    }


    public void OnGet(string? message)
    {
        Message = message;
    }

    public async Task<IActionResult> OnPostLogin(LoginDto body)
    {
        _logger.LogInformation("OnPostLogin");
        try
        {
            // Retrieve the user from the database
            var user = _db.Players.SingleOrDefault(x => x.Username == body.Username);
            if (user == null)
            {
                return new RedirectToPageResult(nameof(Login),
                    new { message = "Passwort oder Benutzername ist falsch" });
            }

            // Verify the password
            if (_pe.VerifyPassword(body.Password, user.PasswordHash))
            {
                // Define user claims
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

            return new RedirectToPageResult(nameof(Login), new { message = "Passwort oder Benutzername ist falsch" });
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return new RedirectToPageResult(nameof(Login), new { message = "Passwort oder Benutzername ist falsch" });
        }
    }


    public async Task<IActionResult> OnPostForgotPasswordAsync(string emailOrPhone)
    {
        var player = _db.Players.FirstOrDefault(x => x.EmailOrPhone == emailOrPhone);

        if (player == null)
        {
            return new RedirectToPageResult(nameof(Login),
                new { ErrorText = "Email oder Telefonnummer existiert nicht" });
        }

        // Generate a verification code
        var code = new Random().Next(100000, 999999).ToString();
        _logger.LogInformation(code);
        player.PasswordResetToken = code;
        player.TokenExpiry = DateTime.UtcNow.AddMinutes(10);
        await _db.SaveChangesAsync();

        // Add entry to verification table
        var verification = new RegistrationVerification
        {
            EmailOrPhone = emailOrPhone,
            VerificationCode = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Purpose = EnvironmentalVariables.PasswordResetPurpose,
        };
        _db.RegistrationVerifications.Add(verification);
        await _db.SaveChangesAsync();

        await _emailService.SendVerificationCodeAsync(player.EmailOrPhone, "Passwort zurücksetzen", code);

        HttpContext.Session.SetString("ResetEmailOrPhone", emailOrPhone);

        return new RedirectToPageResult(nameof(Verification));
    }
}