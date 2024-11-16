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
    public string? ErrorText { get; set; }
    public string? ForgotPasswordErrorText { get; set; }

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


    public void OnGet(string? errorText)
    {
        ErrorText = errorText;
    }

    public async Task<IActionResult> OnPostLogin(LoginDto body)
    {
        _logger.LogInformation("OnPostLogin");
        try
        {
            Player user = _db.Players.Single(x => x.Username == body.Username);
            if (_pe.VerifyPassword(body.Password, user.PasswordHash))
            {
                var claims = new List<Claim>()
                {
                    new(ClaimTypes.Name, user.Id.ToString())
                };
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties()
                {
                    IsPersistent = false
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity), authProperties);

                return new RedirectToPageResult(nameof(Index));
            }

            return new RedirectToPageResult(nameof(Login), new { ErrorText = "Passwort oder Benutzername ist falsch" });
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return new RedirectToPageResult(nameof(Login), new { ErrorText = "Passwort oder Benutzername ist falsch" });
        }
    }

    public async Task<IActionResult> OnPostForgotPasswordAsync(string emailOrPhone)
    {
        _logger.LogInformation("OnPostForgotPassword triggered");

        var player = _db.Players.FirstOrDefault(x => x.EmailOrPhone == emailOrPhone);

        if (player == null)
        {
            ModelState.AddModelError(string.Empty, "Benutzer wurde nicht gefunden.");
            return Page();
        }

        // Generate a verification code (you can also use tokens)
        var code = new Random().Next(100000, 999999).ToString();
        player.PasswordResetToken = code;
        player.TokenExpiry = DateTime.UtcNow.AddMinutes(10);
        await _db.SaveChangesAsync();

        // Send the code via email or SMS
        await _emailService.SendVerificationCodeAsync(player.EmailOrPhone, "Passwort zurücksetzen", code);

        // Save emailOrPhone to session for verification
        HttpContext.Session.SetString("ResetEmailOrPhone", emailOrPhone);

        return new RedirectToPageResult(nameof(Verification));
    }
}