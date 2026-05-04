using System.Text;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace TennisBruck.Services;

public class EmailService
{
    #region Email Service via SMTP

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

        var htmlMessage = $@"
            <div style=""font-family: Arial, sans-serif; color: #333; line-height: 1.6;"">
                <p>Ihr Code lautet: <strong>{verificationCode}</strong></p>
                <hr style=""margin-top: 30px; border: none; border-top: 1px solid #eee;"" />
                <p style=""font-size: 0.9em; color: #666;"">
                    <strong>Hinweis:</strong> Dieser Code ist aus Sicherheitsgründen nur für begrenzte Zeit gültig.<br>
                    Falls du dich gerade nicht anmeldest oder registrierst und diese E-Mail unerwartet erhältst, kannst du sie einfach ignorieren.
                </p>
            </div>";

        message.Body = new TextPart("html")
        {
            Text = htmlMessage
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

    #endregion

    #region Email Service via Resend

    private readonly HttpClient _httpClient;
    private readonly string _apiKey = Environment.GetEnvironmentVariable("RESEND__APIKEY")!;
    private readonly string _from = Environment.GetEnvironmentVariable("RESEND_FROM")!;

    public EmailService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task SendEmailWithResendAsync(string to, string subject, string code)
    {
        var htmlMessage = $@"
            <div style=""font-family: Arial, sans-serif; color: #333; line-height: 1.6;"">
                <p>Ihr Code lautet: <strong>{code}</strong></p>
                <hr style=""margin-top: 30px; border: none; border-top: 1px solid #eee;"" />
                <p style=""font-size: 0.9em; color: #666;"">
                    <strong>Hinweis:</strong> Dieser Code ist aus Sicherheitsgründen nur für begrenzte Zeit gültig.<br>
                    Falls du dich gerade nicht anmeldest oder registrierst und diese E-Mail unerwartet erhältst, kannst du sie einfach ignorieren.
                </p>
            </div>";

        var payload = new
        {
            from = _from,
            to = new[] { to },
            subject,
            html = htmlMessage
        };

        var json = JsonSerializer.Serialize(payload);

        var response = await _httpClient.PostAsync(
            "https://api.resend.com/emails",
            new StringContent(json, Encoding.UTF8, "application/json")
        );

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Email sending failed: {body}");
        }
    }

    #endregion
}