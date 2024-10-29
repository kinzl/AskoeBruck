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
    private TennisContext _db;
    private PasswordEncryption _pe;
    private readonly ILogger<IndexModel> _logger;

    public Login(TennisContext db, PasswordEncryption pe, ILogger<IndexModel> logger)
    {
        _db = db;
        _pe = pe;
        _logger = logger;
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
}