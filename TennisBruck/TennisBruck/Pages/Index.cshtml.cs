using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TennisBruck.Services;
using TennisBruck.wwwroot.Dto;
using TennisDb;

namespace TennisBruck.Pages;

public class IndexModel : PageModel
{
    private CurrentPlayerService _currentPlayerService;
    private TennisContext _db;
    private readonly ILogger<IndexModel> _logger;
    [BindProperty(SupportsGet = true)] public Player? Player { get; set; }

    public IndexModel(ILogger<IndexModel> logger, TennisContext db, CurrentPlayerService currentPlayerService)
    {
        _logger = logger;
        _db = db;
        _currentPlayerService = currentPlayerService;
    }

    public void OnGet()
    {
        Player = _currentPlayerService.GetCurrentUser(HttpContext.User.Identities.ToList().First().Name);
    }

    public IActionResult OnPostLogin(LoginDto body)
    {
        _logger.LogInformation("OnPostLogin");
        return RedirectToPage(nameof(Login));
    }

    public async Task<RedirectToPageResult> OnPostLogout()
    {
        _logger.LogInformation("OnPostLogout");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        // _currentPlayerService.SetPlayer(null);
        return new RedirectToPageResult(nameof(Index));
    }

    public IActionResult OnPostShowMembers()
    {
        _logger.LogInformation("OnPostShowMembers");
        return new RedirectToPageResult(nameof(Members));
    }

    public IActionResult OnPostCourtPlanGrieskirchen()
    {
        _logger.LogInformation("OnPostCourtPlanGrieskirchen");
        return new RedirectToPageResult(nameof(CourtGrieskirchen));
    }

    public IActionResult OnPostShowSettings()
    {
        _logger.LogInformation("OnPostShowSettings");
        return new RedirectToPageResult(nameof(Settings));
    }

    public IActionResult OnPostReserveCourt()
    {
        _logger.LogInformation("OnPostReserveCourt");
        return new RedirectToPageResult(nameof(CourtBruck));
    }
}