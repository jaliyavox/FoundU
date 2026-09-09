using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Common.Pagination;
using FoundU.Application.Notifications.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// Your own notifications. Every route is scoped to the caller - there is no route that reads
/// somebody else's, for any role.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> Mine(
        [FromQuery] NotificationQuery query,
        CancellationToken cancellationToken)
        => Ok(await _notifications.GetForUserAsync(User.GetUserId(), query, cancellationToken));

    /// <summary>What the bell badge shows. Cheap enough to poll.</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount(CancellationToken cancellationToken)
        => Ok(await _notifications.GetUnreadCountAsync(User.GetUserId(), cancellationToken));

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<NotificationDto>> MarkRead(Guid id, CancellationToken cancellationToken)
        => Ok(await _notifications.MarkReadAsync(id, User.GetUserId(), cancellationToken));

    [HttpPost("read-all")]
    public async Task<ActionResult<UnreadCountDto>> MarkAllRead(CancellationToken cancellationToken)
        => Ok(await _notifications.MarkAllReadAsync(User.GetUserId(), cancellationToken));
}
