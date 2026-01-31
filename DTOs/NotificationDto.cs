namespace VoTales.API.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid TriggeredById { get; set; }
    public string TriggeredByName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? RelatedTaleId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
