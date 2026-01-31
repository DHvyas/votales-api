using System.ComponentModel.DataAnnotations;

namespace VoTales.API.Models;

public class Notification
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid TriggeredById { get; set; }

    [Required]
    public string TriggeredByName { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    public Guid? RelatedTaleId { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class NotificationType
{
    public const string Vote = "VOTE";
    public const string Branch = "BRANCH";
    public const string System = "SYSTEM";
}
