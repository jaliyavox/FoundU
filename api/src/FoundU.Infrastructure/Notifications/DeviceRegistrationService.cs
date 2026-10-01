using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Notifications.Dtos;
using FoundU.Domain.Entities;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Notifications;

public class DeviceRegistrationService : IDeviceRegistrationService
{
    private static readonly HashSet<string> SupportedPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "android", "ios", "web"
    };

    private readonly FoundUDbContext _db;

    public DeviceRegistrationService(FoundUDbContext db) => _db = db;

    public async Task RegisterAsync(Guid userId, RegisterDeviceRequest request, CancellationToken cancellationToken = default)
    {
        var token = ValidateToken(request.Token);
        var platform = ValidatePlatform(request.Platform);
        var registration = await _db.DeviceRegistrations
            .SingleOrDefaultAsync(d => d.FcmToken == token, cancellationToken);

        if (registration is null)
        {
            _db.DeviceRegistrations.Add(new DeviceRegistration
            {
                UserId = userId,
                FcmToken = token,
                Platform = platform,
                IsActive = true
            });
        }
        else
        {
            // Tokens belong to a physical app install. A new authenticated account on that device
            // replaces the old association; no endpoint reveals who the previous owner was.
            registration.UserId = userId;
            registration.Platform = platform;
            registration.IsActive = true;
            registration.DeactivatedAt = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnregisterAsync(Guid userId, UnregisterDeviceRequest request, CancellationToken cancellationToken = default)
    {
        var token = ValidateToken(request.Token);
        var registration = await _db.DeviceRegistrations.SingleOrDefaultAsync(
            d => d.UserId == userId && d.FcmToken == token,
            cancellationToken);

        // Intentionally idempotent and does not disclose whether a token belongs to another user.
        if (registration is null)
            return;

        registration.IsActive = false;
        registration.DeactivatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string ValidateToken(string? rawToken)
    {
        var token = rawToken?.Trim();
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 20 or > 4096 || token.Any(char.IsControl))
            throw new ValidationAppException(nameof(RegisterDeviceRequest.Token), "A valid device token is required.");
        return token;
    }

    private static string ValidatePlatform(string? rawPlatform)
    {
        var platform = rawPlatform?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(platform) || !SupportedPlatforms.Contains(platform))
            throw new ValidationAppException(nameof(RegisterDeviceRequest.Platform), "Platform must be android, ios, or web.");
        return platform;
    }
}
