using System.ComponentModel.DataAnnotations;

namespace VoTales.API.Models;

public class Feedback
{
    [Key]
    public Guid Id { get; set; }

    public string? UserEmail { get; set; }

    [Required]
    public string Message { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
