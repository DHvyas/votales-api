using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VoTales.API.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string ResendApiUrl = "https://api.resend.com/emails";

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    private async Task<bool> SendEmailViaApiAsync(string to, string subject, string htmlBody)
    {
        var smtpSettings = _configuration.GetSection("SmtpSettings");
        var apiKey = smtpSettings["Password"];
        var toEmail = smtpSettings["ToEmail"];

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(toEmail))
        {
            _logger.LogWarning("Resend API settings are not configured. Skipping email notification.");
            return false;
        }

        var emailPayload = new
        {
            from = "VoTales <noreply@votales.app>",
            to = new[] { to },
            subject,
            html = htmlBody
        };

        var jsonContent = JsonSerializer.Serialize(emailPayload);
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(ResendApiUrl, content);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Email sent successfully via Resend API to {ToEmail}", to);
            return true;
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        _logger.LogError("Failed to send email via Resend API. Status: {StatusCode}, Response: {Response}", response.StatusCode, responseBody);
        return false;
    }

    public async Task SendCriticalErrorAsync(string title, string details)
    {
        try
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            var toEmail = smtpSettings["ToEmail"];

            if (string.IsNullOrEmpty(toEmail))
            {
                _logger.LogWarning("ToEmail is not configured. Skipping email notification.");
                return;
            }

            var subject = $"🚨 Critical Error: {title}";
            var htmlBody = $"""
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
                """;

            await SendEmailViaApiAsync(toEmail, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send critical error email: {ErrorMessage}. Email sending failed but application continues.", ex.Message);
        }
    }

    public async Task SendFeedbackReceivedAsync(string? userEmail, string message)
    {
        try
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            var toEmail = smtpSettings["ToEmail"];

            if (string.IsNullOrEmpty(toEmail))
            {
                _logger.LogWarning("ToEmail is not configured. Skipping email notification.");
                return;
            }

            var displayEmail = string.IsNullOrWhiteSpace(userEmail) ? "Anonymous" : userEmail;

            var subject = "📬 New Feedback Received";
            var htmlBody = $"""
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
                """;

            await SendEmailViaApiAsync(toEmail, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send feedback received email: {ErrorMessage}. Email sending failed but application continues.", ex.Message);
        }
    }
}
