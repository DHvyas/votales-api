using VoTales.API.Data;
using VoTales.API.DTOs;
using VoTales.API.Models;

namespace VoTales.API.Services;

public class FeedbackService : IFeedbackService
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailService _emailService;

    public FeedbackService(AppDbContext dbContext, IEmailService emailService)
    {
        _dbContext = dbContext;
        _emailService = emailService;
    }

    public async Task<Guid> SubmitFeedbackAsync(CreateFeedbackRequest request)
    {
        var feedback = new Feedback
        {
            Id = Guid.NewGuid(),
            UserEmail = request.Email,
            Message = request.Message,
            SubmittedAt = DateTime.UtcNow
        };

        _dbContext.Feedbacks.Add(feedback);
        await _dbContext.SaveChangesAsync();

        await _emailService.SendFeedbackReceivedAsync(request.Email, request.Message);

        return feedback.Id;
    }
}
