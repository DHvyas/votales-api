using System.ComponentModel.DataAnnotations;

namespace VoTales.API.DTOs;

public class CreateFeedbackRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(10, ErrorMessage = "Message must be at least 10 characters long.")]
    public string Message { get; set; } = string.Empty;
}
