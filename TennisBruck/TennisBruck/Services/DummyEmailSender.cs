namespace TennisBruck.Services;

using Microsoft.AspNetCore.Identity.UI.Services;

public class DummyEmailSender : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        // Gibt die "E-Mail" samt Reset-Link einfach in deiner Entwickler-Konsole aus!
        Console.WriteLine("\n===============================================");
        Console.WriteLine($"📧 NEUE E-MAIL AN: {email}");
        Console.WriteLine($"Betreff: {subject}");
        Console.WriteLine($"Inhalt: {htmlMessage}");
        Console.WriteLine("===============================================\n");

        return Task.CompletedTask;
    }
}