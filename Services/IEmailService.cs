namespace VoTales.API.Services;

public interface IEmailService
{
    Task SendCriticalErrorAsync(string title, string details);
    Task SendFeedbackReceivedAsync(string? userEmail, string message);
}
