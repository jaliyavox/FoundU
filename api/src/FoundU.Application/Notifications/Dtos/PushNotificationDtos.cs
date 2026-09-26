namespace FoundU.Application.Notifications.Dtos;

/// <summary>
/// Minimal server-to-provider payload. Full entity details are fetched from the authorized API
/// after a user taps a notification.
/// </summary>
public record PushNotificationRequest(
    Guid NotificationId,
    Guid UserId,
    string DeviceToken,
    string Title,
    string Body,
    string Type,
    string? EntityId);

public enum PushDeliveryStatus
{
    Delivered,
    InvalidToken,
    RateLimited,
    Unavailable,
    ConfigurationError,
    Failed
}

public record PushDeliveryResult(PushDeliveryStatus Status);
