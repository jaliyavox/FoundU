using FoundU.Application.Notifications.Dtos;

namespace FoundU.Application.Abstractions;

public interface IPushNotificationService
{
    Task<PushDeliveryResult> SendAsync(PushNotificationRequest request, CancellationToken cancellationToken = default);
}
