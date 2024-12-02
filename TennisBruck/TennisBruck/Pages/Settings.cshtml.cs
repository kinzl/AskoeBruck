using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TennisBruck.Extensions;
using TennisBruck.Services;
using TennisBruck.wwwroot.Dto;
using TennisDb;

namespace TennisBruck.Pages;

public class Settings : PageModel
{
    private CurrentUserService _currentUserService;
    private TennisContext _db;
    private PasswordEncryption _pe;
    public string? InfoText { get; set; }
    [BindProperty] public Player Player { get; set; }

    public Settings(CurrentUserService currentUserService, TennisContext db, PasswordEncryption pe)
    {
        _currentUserService = currentUserService;
        _db = db;
        _pe = pe;
    }

    public IActionResult OnGet(string? infoText)
    {
        if (HttpContext.User.Identities.ToList().First().Name == null) return new RedirectToPageResult(nameof(Login));
        InfoText = infoText;
        Player = _currentUserService.GetCurrentUser(HttpContext.User.Identities.ToList().First().Name)!;
        return Page();
    }

    public IActionResult OnPostChangeSettings(RegistrationDto body)
    {
        var player = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        Player = _currentUserService.GetCurrentUser(player)!;
        Player.Firstname = body.Firstname;
        Player.Lastname = body.Lastname;
        Player.EmailOrPhone = body.EmailOrPhone;
        Player.Username = body.Username;
        _db.SaveChanges();

        return RedirectToPage(nameof(Settings), new { infoText = "Daten gespeichert" });
    }

    public IActionResult OnPostChangePassword(string newPassword, string newPasswordRepeat)
    {
        if (newPassword != newPasswordRepeat)
            return RedirectToPage(nameof(Settings), new { infoText = "Passwörter stimmen nicht überein" });
        var player = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        Player = _currentUserService.GetCurrentUser(player)!;
        Player.PasswordHash = _pe.HashPassword(newPassword);
        _db.SaveChanges();
        return RedirectToPage(nameof(Settings), new { infoText = "Neues Passwort gespeichert" });
    }
}