using Microsoft.AspNetCore.Identity.UI.Services;
using Resend;

namespace TennisBruck.Services;

public class EmailSender(ResendClient resend) : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        await resend.EmailSendAsync(new EmailMessage
        {
            From = "onboarding@resend.dev",
            To = { email },
            Subject = subject,
            HtmlBody = htmlMessage
        });
    }
}