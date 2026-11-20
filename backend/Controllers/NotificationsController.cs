using backend.Api;
using backend.Api.Contracts;
using backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(ClaimsDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<NotificationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<NotificationResponse>>> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!ClaimAccess.TryGetUserId(User, out var userId))
        {
            return Unauthorized();
        }

        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            return BadRequest(new ProblemDetails { Title = "Page must be positive and pageSize must be between 1 and 100." });
        }

        var query = db.Notifications.AsNoTracking().Where(notification => notification.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(notification => !notification.IsRead);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(notification => new NotificationResponse(
                notification.Id,
                notification.ClaimId,
                notification.Title,
                notification.Message,
                notification.IsRead,
                notification.CreatedAt,
                notification.ReadAt))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<NotificationResponse>(items, page, pageSize, totalCount));
    }

    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadNotificationCountResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadNotificationCountResponse>> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        if (!ClaimAccess.TryGetUserId(User, out var userId))
        {
            return Unauthorized();
        }

        var count = await db.Notifications.CountAsync(
            notification => notification.UserId == userId && !notification.IsRead,
            cancellationToken);
        return Ok(new UnreadNotificationCountResponse(count));
    }

    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType<NotificationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationResponse>> MarkRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!ClaimAccess.TryGetUserId(User, out var userId))
        {
            return Unauthorized();
        }

        var notification = await db.Notifications.SingleOrDefaultAsync(
            item => item.Id == id && item.UserId == userId,
            cancellationToken);
        if (notification is null)
        {
            return NotFound();
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = ClaimsController.CurrentDatabaseTimestamp();
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new NotificationResponse(
            notification.Id,
            notification.ClaimId,
            notification.Title,
            notification.Message,
            notification.IsRead,
            notification.CreatedAt,
            notification.ReadAt));
    }

    [HttpPatch("read-all")]
    [ProducesResponseType<MarkNotificationsReadResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MarkNotificationsReadResponse>> MarkAllRead(
        CancellationToken cancellationToken)
    {
        if (!ClaimAccess.TryGetUserId(User, out var userId))
        {
            return Unauthorized();
        }

        var readAt = ClaimsController.CurrentDatabaseTimestamp();
        var updatedCount = await db.Notifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(notification => notification.IsRead, true)
                .SetProperty(notification => notification.ReadAt, readAt), cancellationToken);

        return Ok(new MarkNotificationsReadResponse(updatedCount));
    }
}
