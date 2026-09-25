using FoundU.Application.Common.Pagination;
using FoundU.Application.Notifications.Dtos;
using FoundU.Domain.Enums;

namespace FoundU.Application.Abstractions;

/// <summary>
/// Tells someone that something happened to their report or claim.
///
/// Everything here is addressed to one person: a notification is not an event log, it is a
/// message with a recipient. Writing one is part of the same transaction as the thing it
/// describes - a claim that was approved but whose owner was never told is a bug that only
/// shows up as a person waiting.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Queues a notification onto the current unit of work. It is saved by the caller's own
    /// SaveChanges, so the notification and the event it describes commit together.
    /// </summary>
    void Queue(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null);

    Task<PagedResult<NotificationDto>> GetForUserAsync(Guid userId, NotificationQuery query, CancellationToken cancellationToken = default);

    Task<UnreadCountDto> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marks one as read. Only the recipient may - enforced in the service.</summary>
    Task<NotificationDto> MarkReadAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Clears the badge in one go. Returns how many were still unread.</summary>
    Task<UnreadCountDto> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
