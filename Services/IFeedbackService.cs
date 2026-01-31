using VoTales.API.DTOs;

namespace VoTales.API.Services;

public interface IFeedbackService
{
    Task<Guid> SubmitFeedbackAsync(CreateFeedbackRequest request);
}
