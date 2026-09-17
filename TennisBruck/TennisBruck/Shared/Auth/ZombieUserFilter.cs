using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TennisBruck.Shared.Auth;

public class ZombieUserFilter(CurrentPlayerService currentPlayerService, SignInManager<IdentityUser> signInManager)
    : IAsyncPageFilter
{
    public async Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
    {
        await Task.CompletedTask;
    }

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        if (user.Identity != null && user.Identity.IsAuthenticated)
        {
            var currentPlayer = currentPlayerService.GetCurrentUser();

            if (currentPlayer == null)
            {
                await signInManager.SignOutAsync();
                context.Result = new RedirectToPageResult("/Account/Login", new { area = "Identity" });
                return;
            }
        }

        await next();
    }
}
