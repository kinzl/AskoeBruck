using Microsoft.AspNetCore.Identity.UI.Services;
using Resend;

namespace TennisBruck.Services;

public class EmailSender(ResendClient resend) : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var extendedMessage = $@"
            <div style=""font-family: Arial, sans-serif; color: #333; line-height: 1.6;"">
                {htmlMessage}
                <hr style=""margin-top: 30px; border: none; border-top: 1px solid #eee;"" />
                <p style=""font-size: 0.9em; color: #666;"">
                    <strong>Hinweis:</strong> Dieser Link oder Code ist aus Sicherheitsgründen nur für begrenzte Zeit gültig.<br>
                    Falls du dich gerade nicht anmeldest oder registrierst und diese E-Mail unerwartet erhältst, kannst du sie einfach ignorieren.
                </p>
            </div>";

        await resend.EmailSendAsync(new EmailMessage
        {
            From = "noreply@tennis-bruck.at",
            To = { email },
            Subject = subject,
            HtmlBody = extendedMessage
        });
    }
}