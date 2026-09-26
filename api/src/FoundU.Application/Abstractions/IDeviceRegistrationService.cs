using FoundU.Application.Notifications.Dtos;

namespace FoundU.Application.Abstractions;

public interface IDeviceRegistrationService
{
    Task RegisterAsync(Guid userId, RegisterDeviceRequest request, CancellationToken cancellationToken = default);
    Task UnregisterAsync(Guid userId, UnregisterDeviceRequest request, CancellationToken cancellationToken = default);
}
