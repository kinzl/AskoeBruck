using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TennisBruck.Services;
using TennisBruck.wwwroot.Dto;

namespace TennisBruck.Pages;

public class Registration : PageModel
{
    private readonly EmailService _emailService;
    private readonly ILogger<Registration> _logger;

    public Registration(EmailService emailService, ILogger<Registration> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<IActionResult> OnPostRegisterAsync(string emailOrPhone)
    {
        var code = GenerateVerificationCode();
        _logger.LogInformation(code);

        // Store the code in session
        HttpContext.Session.SetString("VerificationCode", code);

        await _emailService.SendEmailAsync(emailOrPhone, "Your Verification Code", code);

        return new RedirectToPageResult(nameof(Verification));
    }

    public string GenerateVerificationCode()
    {
        return new Random().Next(100000, 999999).ToString();
    }
}