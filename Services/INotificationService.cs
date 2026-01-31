using VoTales.API.DTOs;

namespace VoTales.API.Services;

public interface INotificationService
{
    Task CreateNotificationAsync(
        Guid userId,
        string type,
        string message,
        Guid? relatedTaleId,
        Guid triggeredById,
        string triggeredByName);

    Task<List<NotificationDto>> GetUnreadNotificationsAsync(Guid userId);

    Task MarkAsReadAsync(Guid notificationId);

    Task MarkAllAsReadAsync(Guid userId);
}
