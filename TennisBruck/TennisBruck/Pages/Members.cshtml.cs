using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TennisBruck.Services;
using TennisBruck.wwwroot.Dto;
using TennisDb;

namespace TennisBruck.Pages;

public class Members : PageModel
{
    private TennisContext _db;
    private CurrentUserService CurrentUserService { get; set; }
    private readonly ILogger<IndexModel> _logger;
    public Player LoggedInPlayer { get; set; }
    public List<Player> AllPlayers { get; set; }
    public string? InfoBox { get; set; }

    public Members(TennisContext db, ILogger<IndexModel> logger, CurrentUserService currentUserService)
    {
        _db = db;
        _logger = logger;
        CurrentUserService = currentUserService;
    }

    public RedirectToPageResult? OnGet(string? infoBox)
    {
        if (HttpContext.User.Identities.ToList().First().Name == null) return new RedirectToPageResult(nameof(Login));
        InfoBox = infoBox;
        LoggedInPlayer = CurrentUserService.GetCurrentUser(HttpContext.User.Identities.ToList().First().Name)!;
        _logger.LogInformation("Id {Name} Signed in", HttpContext.User.Identities.ToList().First().Name);
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
            PasswordHash = password,
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
        return new RedirectToPageResult(nameof(Members));
    }
}