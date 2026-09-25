using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Common.Pagination;
using FoundU.Application.Notifications.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Notifications;

/// <summary>
/// Notifications, written alongside the thing they describe.
///
/// <see cref="Queue"/> adds the row but does not save: the caller's SaveChanges commits both,
/// so there is no state where a claim was approved and nobody was told. The service is scoped
/// and shares the caller's DbContext, which is what makes that work.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly FoundUDbContext _db;

    public NotificationService(FoundUDbContext db)
    {
        _db = db;
    }

    public void Queue(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
        });
    }

    public async Task<PagedResult<NotificationDto>> GetForUserAsync(
        Guid userId,
        NotificationQuery query,
        CancellationToken cancellationToken = default)
    {
        var notifications = _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (query.UnreadOnly == true)
        {
            notifications = notifications.Where(n => !n.IsRead);
        }

        // Newest first: a notification list is read from the top and abandoned partway down.
        var ordered = notifications.OrderByDescending(n => n.CreatedAt);

        var totalCount = await ordered.CountAsync(cancellationToken);

        var items = await ordered
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(n => new NotificationDto(
                n.Id,
                n.Type.ToString(),
                n.Title,
                n.Message,
                n.IsRead,
                n.RelatedEntityType,
                n.RelatedEntityId,
                n.CreatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<NotificationDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<UnreadCountDto> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => new(await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken));

    public async Task<NotificationDto> MarkReadAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Notification '{id}' was not found.");

        // Not "forbidden": whose notification it is has already been decided by whose it is
        // addressed to, and someone probing ids should learn nothing from the difference.
        if (notification.UserId != userId)
        {
            throw new NotFoundAppException($"Notification '{id}' was not found.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new NotificationDto(
            notification.Id,
            notification.Type.ToString(),
            notification.Title,
            notification.Message,
            notification.IsRead,
            notification.RelatedEntityType,
            notification.RelatedEntityId,
            notification.CreatedAt);
    }

    public async Task<UnreadCountDto> MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // One statement rather than loading every row to flip a flag on it.
        var cleared = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, now)
                    .SetProperty(n => n.UpdatedAt, now),
                cancellationToken);

        return new UnreadCountDto(cleared);
    }
}
