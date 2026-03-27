using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TennisBruck.Pages.Filter;

public class ZombieUserFilter(CurrentPlayerService currentPlayerService, SignInManager<IdentityUser> signInManager)
    : IAsyncPageFilter
{
    public async Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
    {
        // Wird aufgerufen, bevor der Handler ausgewählt wird (brauchen wir hier nicht)
        await Task.CompletedTask;
    }

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        // Wir greifen nur ein, wenn das System DENKT, der User sei eingeloggt
        if (user.Identity != null && user.Identity.IsAuthenticated)
        {
            var currentPlayer = currentPlayerService.GetCurrentUser();

            // GEISTER-ALARM! Der Keks ist da, aber der Spieler fehlt in der Datenbank
            if (currentPlayer == null)
            {
                // Keks zerstören
                await signInManager.SignOutAsync();

                // User auf die Login-Seite umleiten
                context.Result = new RedirectToPageResult("/Account/Login", new { area = "Identity" });
                return; // Wir brechen hier ab, die eigentliche Seite wird gar nicht erst geladen!
            }
        }

        // Alles okay! Lass den User die Seite normal laden.
        await next();
    }
}