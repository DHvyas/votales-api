using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoTales.API.Services;

namespace VoTales.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUnreadNotifications()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("Unable to determine user identity.");
        }

        var notifications = await _notificationService.GetUnreadNotificationsAsync(userId);
        return Ok(notifications);
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await _notificationService.MarkAsReadAsync(id);
        return Ok();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("Unable to determine user identity.");
        }

        await _notificationService.MarkAllAsReadAsync(userId);
        return Ok();
    }
}
