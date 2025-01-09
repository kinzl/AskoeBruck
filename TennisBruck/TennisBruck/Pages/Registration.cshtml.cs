using System.Text.RegularExpressions;
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
        // Check for existing users
        if (_db.Players.Any(x => x.EmailOrPhone == body.EmailOrPhone))
            return new RedirectToPageResult(nameof(Registration),
                new { errorText = "Email oder Telefonnummer existiert bereits" });
        if (_db.Players.Any(x => x.Username == body.Username))
            return new RedirectToPageResult(nameof(Registration),
                new { errorText = "Benutzername existiert bereits" });

        // Generate a verification code
        var code = GenerateVerificationCode();
        _logger.LogInformation(code);

        // Insert the data into the verification table
        var verificationEntry = new RegistrationVerification
        {
            Firstname = body.Firstname,
            Lastname = body.Lastname,
            EmailOrPhone = body.EmailOrPhone,
            Username = body.Username,
            PasswordHash = _pe.HashPassword(body.Password),
            VerificationCode = code,
            Purpose = EnvironmentalVariables.RegistrationPurpose,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10) // Set expiration time
        };
        _db.RegistrationVerifications.Add(verificationEntry);
        await _db.SaveChangesAsync();

        // Send the verification code
        if (Regex.IsMatch(body.EmailOrPhone, EnvironmentalVariables.PhoneRegex))
        {
            // _smsService.SendSms(body.EmailOrPhone);
        }
        else
        {
            await _emailService.SendVerificationCodeAsync(body.EmailOrPhone, "Verifizierungs Code", code);
        }

        return new RedirectToPageResult(nameof(Verification), new { emailOrPhone = body.EmailOrPhone });
    }


    public string GenerateVerificationCode()
    {
        return new Random().Next(100000, 999999).ToString();
    }
}