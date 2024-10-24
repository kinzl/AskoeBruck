using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TennisDb;

namespace TennisBruck.Pages;

public class Verification(TennisContext db, ILogger<Verification> logger) : PageModel
{
    public string? ErrorText { get; set; }

    public IActionResult OnPostVerify(string inputCode)
    {
        var storedCode = HttpContext.Session.GetString("VerificationCode");

        if (storedCode == inputCode)
        {
            var player = new Player()
            {
                Firstname = HttpContext.Session.GetString("Firstname")!,
                Lastname = HttpContext.Session.GetString("Lastname")!,
                EmailOrPhone = HttpContext.Session.GetString("EmailOrPhone")!,
                Username = HttpContext.Session.GetString("Username")!,
                PasswordHash = HttpContext.Session.GetString("PasswordHash")!,
                IsAdmin = false
            };
            db.Players.Add(player);
            db.SaveChanges();
            logger.LogInformation("New Player Created: {0}", player);

            HttpContext.Session.Remove("VerificationCode");
            HttpContext.Session.Remove("Firstname");
            HttpContext.Session.Remove("Lastname");
            HttpContext.Session.Remove("EmailOrPhone");
            HttpContext.Session.Remove("Username");
            HttpContext.Session.Remove("PasswordHash");

            _ = Login(player);
        }
        else
        {
            // Code is incorrect
            ErrorText = "Invalid verification code.";
            return Page();
        }

        return Page();
    }

    private async Task<IActionResult> Login(Player player)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, player.Id.ToString())
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
}
