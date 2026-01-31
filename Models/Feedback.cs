using System.ComponentModel.DataAnnotations;

namespace VoTales.API.Models;

public class Feedback
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string UserEmail { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
