using System.ComponentModel.DataAnnotations;

namespace VoTales.API.DTOs;

public class CreateFeedbackRequest
{
    [EmailAddress]
    public string? Email { get; set; }

    [Required]
    public string Message { get; set; } = string.Empty;
}
