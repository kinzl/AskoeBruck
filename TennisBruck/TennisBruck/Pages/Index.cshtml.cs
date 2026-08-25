namespace TennisBruck.Pages;

public class IndexModel(CurrentPlayerService currentPlayerService)
    : PageModel
{
    [BindProperty(SupportsGet = true)] public Player? CurrentPlayer { get; set; }

    public void OnGet()
    {
        CurrentPlayer = currentPlayerService.GetCurrentUser();
    }
}