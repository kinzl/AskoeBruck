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
    private PlayerService _playerService;
    private TennisContext _db;
    private readonly ILogger<IndexModel> _logger;
    [BindProperty(SupportsGet = true)] public Player? Player { get; set; }

    public IndexModel(ILogger<IndexModel> logger, TennisContext db, PlayerService playerService)
    {
        _logger = logger;
        _db = db;
        _playerService = playerService;
    }

    public void OnGet()
    {
        Player = _playerService.GetPlayer(HttpContext.User.Identities.ToList().First().Name);
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
        _playerService.SetPlayer(null);
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
    
    public IActionResult OnPostPunch()
    {
        _logger.LogInformation("OnPostPunch");
        return new RedirectToPageResult(nameof(Punch));
    }
}