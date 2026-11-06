using System.Net;
using System.Net.Mail;

namespace NbaTracker.Api.Services;

public class EmailService
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _password;
    private readonly string _fromEmail;

    public EmailService(IConfiguration config)
    {
        // ASP.NET Core maps env var Smtp__X -> config key Smtp:X via __ separator
        _host = config["Smtp:Host"] ?? "smtp.gmail.com";
        _port = int.TryParse(config["Smtp:Port"], out var port) ? port : 587;
        _user = config["Smtp:User"]
            ?? throw new InvalidOperationException("Smtp__User env var must be configured");
        _password = config["Smtp:Password"]
            ?? throw new InvalidOperationException("Smtp__Password env var must be configured");
        _fromEmail = config["Smtp:FromEmail"] ?? _user;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, CancellationToken ct)
    {
        using var client = new SmtpClient(_host, _port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_user, _password)
        };

        using var message = new MailMessage(_fromEmail, toEmail)
        {
            Subject = "Reset your NBA Lines Tracker password",
            Body = $"We received a request to reset your password.\n\n"
                 + $"Reset it here: {resetUrl}\n\n"
                 + "This link expires in 1 hour. If you didn't request this, you can ignore this email.",
            IsBodyHtml = false
        };

        await client.SendMailAsync(message, ct);
    }
}
