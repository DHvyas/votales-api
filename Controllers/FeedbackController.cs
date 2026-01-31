using Microsoft.AspNetCore.Mvc;
using VoTales.API.DTOs;
using VoTales.API.Services;

namespace VoTales.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedbackService;

    public FeedbackController(IFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    /// <summary>
    /// Submit feedback (anonymous access allowed so users can report login issues)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] CreateFeedbackRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var feedbackId = await _feedbackService.SubmitFeedbackAsync(request);
        return Ok(new { id = feedbackId, message = "Thank you for your feedback!" });
    }
}
