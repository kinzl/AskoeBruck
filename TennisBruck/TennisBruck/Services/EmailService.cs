using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace TennisBruck.Services;

public class EmailService
{
    private readonly string _smtpServer = Environment.GetEnvironmentVariable("SMTPSERVER")!; // Your SMTP server
    private readonly int _smtpPort = 587;
    private readonly string _smtpUser = Environment.GetEnvironmentVariable("EMAIL")!; // Your Gmail address
    private readonly string _smtpPass = Environment.GetEnvironmentVariable("PASSWORD")!; // Your Gmail password

    public async Task SendVerificationCodeAsync(string toEmail, string subject, string verificationCode)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("ASKÖ Bruck", _smtpUser));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = subject;

        message.Body = new TextPart("plain")
        {
            Text = "Ihr Code lautet: " + verificationCode
        };

        var client = new SmtpClient();
        try
        {
            // Connect to the SMTP server
            await client.ConnectAsync(_smtpServer, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);

            // Authenticate
            await client.AuthenticateAsync(_smtpUser, _smtpPass);

            // Send the email
            await client.SendAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send email: {ex.Message}");
        }
        finally
        {
            // Disconnect from the SMTP server
            await client.DisconnectAsync(true);
        }
    }
}