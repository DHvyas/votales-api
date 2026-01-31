using MailKit.Net.Smtp;
using MimeKit;

namespace VoTales.API.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendCriticalErrorAsync(string title, string details)
    {
        try
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");

            var host = smtpSettings["Host"];
            var port = int.Parse(smtpSettings["Port"] ?? "587");
            var username = smtpSettings["Username"];
            var password = smtpSettings["Password"];
            var toEmail = smtpSettings["ToEmail"];

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(toEmail))
            {
                _logger.LogWarning("SMTP settings are not configured. Skipping email notification.");
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("VoTales API Alert", username));
            message.To.Add(new MailboxAddress("Admin", toEmail));
            message.Subject = $"🚨 Critical Error: {title}";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $"""
                    <html>
                    <body style="font-family: Arial, sans-serif; padding: 20px;">
                        <h2 style="color: #d9534f;">🚨 Critical Error in VoTales API</h2>
                        <p><strong>Title:</strong> {title}</p>
                        <hr style="border: 1px solid #ddd;" />
                        <h3>Details:</h3>
                        <pre style="background-color: #f5f5f5; padding: 15px; border-radius: 5px; overflow-x: auto;">{details}</pre>
                        <hr style="border: 1px solid #ddd;" />
                        <p style="color: #888; font-size: 12px;">
                            Sent at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
                        </p>
                    </body>
                    </html>
                    """
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Critical error email sent successfully to {ToEmail}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send critical error email. Email sending failed but application continues.");
        }
    }

    public async Task SendFeedbackReceivedAsync(string? userEmail, string message)
    {
        try
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");

            var host = smtpSettings["Host"];
            var port = int.Parse(smtpSettings["Port"] ?? "587");
            var username = smtpSettings["Username"];
            var password = smtpSettings["Password"];
            var toEmail = smtpSettings["ToEmail"];

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(toEmail))
            {
                _logger.LogWarning("SMTP settings are not configured. Skipping email notification.");
                return;
            }

            var displayEmail = string.IsNullOrWhiteSpace(userEmail) ? "Anonymous" : userEmail;

            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress("VoTales Feedback", username));
            mimeMessage.To.Add(new MailboxAddress("Admin", toEmail));
            mimeMessage.Subject = "📬 New Feedback Received";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $"""
                    <html>
                    <body style="font-family: Arial, sans-serif; padding: 20px;">
                        <h2 style="color: #5bc0de;">📬 New Feedback Received</h2>
                        <p><strong>From:</strong> {displayEmail}</p>
                        <hr style="border: 1px solid #ddd;" />
                        <h3>Message:</h3>
                        <pre style="background-color: #f5f5f5; padding: 15px; border-radius: 5px; overflow-x: auto;">{message}</pre>
                        <hr style="border: 1px solid #ddd;" />
                        <p style="color: #888; font-size: 12px;">
                            Received at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
                        </p>
                    </body>
                    </html>
                    """
            };

            mimeMessage.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(mimeMessage);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Feedback received email sent successfully to {ToEmail}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send feedback received email. Email sending failed but application continues.");
        }
    }
}
