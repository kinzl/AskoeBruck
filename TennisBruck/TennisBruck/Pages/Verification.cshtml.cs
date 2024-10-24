using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TennisBruck.Pages;

public class Verification : PageModel
{
    public string? ErrorText { get; set; }

    public IActionResult OnPostVerify(string inputCode)
    {
        var storedCode = HttpContext.Session.GetString("VerificationCode");

        if (storedCode == inputCode)
        {
            // Success: Clear the session after verification
            HttpContext.Session.Remove("VerificationCode");

            //ToDo: Log the user in and store the user's information in the database

            return new RedirectToPageResult(nameof(Index));
        }
        else // !!!!!else is very important
        {
            // Code is incorrect
            ErrorText = "Invalid verification code.";
            return Page();
        }
    }
}