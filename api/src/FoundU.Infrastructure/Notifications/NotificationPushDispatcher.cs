using FoundU.Application.Abstractions;
using FoundU.Application.Notifications.Dtos;
using FoundU.Domain.Entities;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FoundU.Infrastructure.Notifications;

/// <summary>Post-commit dispatcher for persisted FoundU notifications.</summary>
public class NotificationPushDispatcher
{
    private readonly FoundUDbContext _db;
    private readonly IPushNotificationService _push;
    private readonly ILogger<NotificationPushDispatcher> _logger;

    public NotificationPushDispatcher(
        FoundUDbContext db,
        IPushNotificationService push,
        ILogger<NotificationPushDispatcher> logger)
    {
        _db = db;
        _push = push;
        _logger = logger;
    }

    public async Task DispatchAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        var registrations = await _db.DeviceRegistrations
            .Where(d => d.UserId == notification.UserId && d.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var registration in registrations)
        {
            try
            {
                _logger.LogInformation("Push requested. NotificationId: {NotificationId}, UserId: {UserId}", notification.Id, notification.UserId);
                var payload = CreateSafePayload(notification);
                var result = await _push.SendAsync(new PushNotificationRequest(
                    notification.Id,
                    notification.UserId,
                    registration.FcmToken,
                    payload.Title,
                    payload.Body,
                    notification.Type.ToString(),
                    notification.RelatedEntityId?.ToString()), cancellationToken);

                if (result.Status == PushDeliveryStatus.InvalidToken)
                {
                    registration.IsActive = false;
                    registration.DeactivatedAt = DateTime.UtcNow;
                    _logger.LogInformation("Invalid push token disabled. NotificationId: {NotificationId}, UserId: {UserId}", notification.Id, notification.UserId);
                }
                else if (result.Status == PushDeliveryStatus.RateLimited)
                {
                    _logger.LogWarning("Push rate limited. NotificationId: {NotificationId}, UserId: {UserId}", notification.Id, notification.UserId);
                }
            }
            catch (Exception ex)
            {
                // This boundary is deliberately non-authoritative: a push failure must never roll
                // back an already-persisted business operation or its in-app notification.
                _logger.LogWarning(ex, "Push dispatch failed safely. NotificationId: {NotificationId}, UserId: {UserId}", notification.Id, notification.UserId);
            }
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(cancellationToken);
    }

    private static (string Title, string Body) CreateSafePayload(Notification notification)
        => notification.Type switch
        {
            Domain.Enums.NotificationType.PossibleMatchFound => ("Possible match", "A possible match is available in FoundU."),
            Domain.Enums.NotificationType.VerificationQuestionAvailable => ("Claim update", "Verification questions are ready."),
            Domain.Enums.NotificationType.RevisionRequested => ("Claim update", "Your claim needs staff-requested changes."),
            Domain.Enums.NotificationType.ClaimApproved => ("Claim update", "Your claim status has changed."),
            Domain.Enums.NotificationType.ClaimRejected => ("Claim update", "Your claim status has changed."),
            Domain.Enums.NotificationType.CollectionInstructions => ("Collection update", "Your item is ready for collection."),
            Domain.Enums.NotificationType.ItemReportedFound => ("Report update", "There is an update about your lost item."),
            Domain.Enums.NotificationType.MessageReceived => ("New message", "You have a new message in FoundU."),
            _ => ("FoundU update", "You have a new FoundU notification.")
        };
}
