using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace TennisBruck.Services;

public class EmailService
{
    private readonly string _smtpServer = Environment.GetEnvironmentVariable("SMTPSERVER")!;
    private readonly int _smtpPort = 587;
    private readonly string _smtpUser = Environment.GetEnvironmentVariable("EMAIL")!;
    private readonly string _smtpPass = Environment.GetEnvironmentVariable("PASSWORD")!;

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
            Console.WriteLine("Connecting to smtp server");
            await client.ConnectAsync(_smtpServer, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            Console.WriteLine("Connected");
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