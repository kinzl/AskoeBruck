using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TennisBruck.Extensions;
using TennisBruck.Services;
using TennisBruck.wwwroot.Dto;
using TennisDb;

namespace TennisBruck.Pages;

public class Registration : PageModel
{
    private readonly EmailService _emailService;
    private readonly ILogger<Registration> _logger;
    private TennisContext _db;
    private PasswordEncryption _pe;
    public string? ErrorText { get; set; }

    public Registration(EmailService emailService, ILogger<Registration> logger, TennisContext db,
        PasswordEncryption pe)
    {
        _emailService = emailService;
        _logger = logger;
        _db = db;
        _pe = pe;
    }

    public void OnGet(string? errorText)
    {
        ErrorText = errorText;
    }

    public async Task<IActionResult> OnPostRegisterAsync(RegistrationDto body)
    {
        if (_db.Players.Any(x => x.EmailOrPhone == body.EmailOrPhone))
            return new RedirectToPageResult(nameof(Registration),
                new { errorText = "Email oder Telefonnummer existiert bereits" });
        if (_db.Players.Any(x => x.Username == body.Username))
            return new RedirectToPageResult(nameof(Registration),
                new { errorText = "Benutzername existiert bereits" });

        var code = GenerateVerificationCode();
        _logger.LogInformation(code);

        // Store the code in session
        //Gson not compatible with .NET 9
        HttpContext.Session.SetString("VerificationCode", code);
        HttpContext.Session.SetString("Firstname", body.Firstname);
        HttpContext.Session.SetString("Lastname", body.Lastname);
        HttpContext.Session.SetString("EmailOrPhone", body.EmailOrPhone);
        HttpContext.Session.SetString("Username", body.Username);
        HttpContext.Session.SetString("PasswordHash", _pe.HashPassword(body.Password));

        await _emailService.SendEmailAsync(body.EmailOrPhone, "Your Verification Code", code);

        return new RedirectToPageResult(nameof(Verification));
    }

    public string GenerateVerificationCode()
    {
        return new Random().Next(100000, 999999).ToString();
    }
}