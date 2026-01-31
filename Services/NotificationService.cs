using Microsoft.EntityFrameworkCore;
using VoTales.API.Data;
using VoTales.API.DTOs;
using VoTales.API.Models;

namespace VoTales.API.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _dbContext;

    public NotificationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateNotificationAsync(
        Guid userId,
        string type,
        string message,
        Guid? relatedTaleId,
        Guid triggeredById,
        string triggeredByName)
    {
        // Don't notify users of their own actions
        if (userId == triggeredById)
        {
            return;
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TriggeredById = triggeredById,
            TriggeredByName = triggeredByName,
            Type = type,
            Message = message,
            RelatedTaleId = relatedTaleId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<NotificationDto>> GetUnreadNotificationsAsync(Guid userId)
    {
        return await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                TriggeredById = n.TriggeredById,
                TriggeredByName = n.TriggeredByName,
                Type = n.Type,
                Message = n.Message,
                RelatedTaleId = n.RelatedTaleId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var notification = await _dbContext.Notifications.FindAsync(notificationId);

        if (notification is not null)
        {
            notification.IsRead = true;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));
    }
}
