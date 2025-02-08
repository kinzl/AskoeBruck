using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TennisBruck.Extensions;
using TennisBruck.Services;
using TennisBruck.wwwroot.Dto;
using TennisDb;

namespace TennisBruck.Pages;

[Authorize]
public class Members : PageModel
{
    private TennisContext _db;
    private CurrentPlayerService CurrentPlayerService { get; set; }
    private readonly ILogger<IndexModel> _logger;
    public Player LoggedInPlayer { get; set; }
    public List<Player> AllPlayers { get; set; }
    public string? InfoBox { get; set; }
    private PasswordEncryption _pe;

    public Members(TennisContext db, ILogger<IndexModel> logger, CurrentPlayerService currentPlayerService,
        PasswordEncryption pe)
    {
        _db = db;
        _logger = logger;
        CurrentPlayerService = currentPlayerService;
        _pe = pe;
    }

    public RedirectToPageResult? OnGet(string? infoBox)
    {
        InfoBox = infoBox;
        LoggedInPlayer = CurrentPlayerService.GetCurrentUser(HttpContext.User.Identities.ToList().First().Name)!;
        AllPlayers = _db.Players.ToList();
        return null;
    }

    public IActionResult OnPostCreateUser(RegistrationDto body)
    {
        _logger.LogInformation("OnPostCreateUser");
        string password = "askoebruck";
        var player = new Player
        {
            Firstname = body.Firstname,
            Lastname = body.Lastname,
            EmailOrPhone = body.EmailOrPhone,
            PasswordHash = _pe.HashPassword(password),
            Username = body.Username,
            IsAdmin = false,
            IsPlayingGrieskirchen = false
        };
        _db.Players.Add(player);
        _db.SaveChanges();

        return new RedirectToPageResult(nameof(Members),
            new { infoBox = $"Benutzer wurde erstellt, das Passwort ist {password}" });
    }

    public IActionResult OnPostDeleteUser(int playerId)
    {
        _logger.LogInformation("OnPostDeleteUser");
        var player = _db.Players.Single(x => x.Id == playerId);
        _db.Players.Remove(player);
        _db.SaveChanges();
        return RedirectToPage(nameof(Members));
    }

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }

    public IActionResult OnPostChangeAdmin(int user)
    {
        _logger.LogInformation($"Toggling admin status for User ID: {user}");

        var player = _db.Players.FirstOrDefault(p => p.Id == user);
        if (player != null)
        {
            player.IsAdmin = !player.IsAdmin; // Toggle admin status
            _db.SaveChanges();
        }

        return RedirectToPage();
    }
}