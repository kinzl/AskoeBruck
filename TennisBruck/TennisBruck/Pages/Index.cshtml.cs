using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages;

namespace TennisBruck.Pages;

public class IndexModel(ILogger<IndexModel> logger, CurrentPlayerService currentPlayerService)
    : PageModel
{
    [BindProperty(SupportsGet = true)] public Player? CurrentPlayer { get; set; }

    public void OnGet()
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
    }

    public IActionResult OnPostLogin(LoginDto body)
    {
        logger.LogInformation("OnPostLogin");
        return RedirectToPage(nameof(Login));
    }

    public async Task<RedirectToPageResult> OnPostLogout()
    {
        logger.LogInformation("OnPostLogout");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return new RedirectToPageResult(nameof(Index));
    }

    public IActionResult OnPostShowMembers()
    {
        logger.LogInformation("OnPostShowMembers");
        return new RedirectToPageResult(nameof(Members));
    }

    public IActionResult OnPostHallplan()
    {
        logger.LogInformation("OnPostHallplan");
        return new RedirectToPageResult(nameof(Hallplan));
    }

    public IActionResult OnPostShowSettings()
    {
        logger.LogInformation("OnPostShowSettings");
        return new RedirectToPageResult(nameof(Settings));
    }

    public IActionResult OnPostReserveCourt()
    {
        logger.LogInformation("OnPostReserveCourt");
        return new RedirectToPageResult(nameof(CourtBruck));
    }

    public IActionResult OnPostChampionship()
    {
        logger.LogInformation("OnPostChampionship");
        return new RedirectToPageResult(nameof(Championship));
    }
}