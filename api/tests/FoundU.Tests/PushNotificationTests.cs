using FoundU.Application.Abstractions;
using FoundU.Application.Notifications.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FoundU.Tests;

public sealed class PushNotificationTests
{
    [Fact]
    public async Task Registration_UpdatesOwnTokenWithoutDuplicatingIt_AndUnregistersOnlyOwner()
    {
        await using var db = CreateDb();
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        var service = new DeviceRegistrationService(db);
        const string token = "fcm-token-abcdefghijklmnopqrstuvwxyz-0123456789";

        await service.RegisterAsync(firstUser, new(token, "android"));
        await service.RegisterAsync(firstUser, new(token, "ios"));
        await service.UnregisterAsync(secondUser, new(token));

        var registration = Assert.Single(db.DeviceRegistrations);
        Assert.Equal(firstUser, registration.UserId);
        Assert.Equal("ios", registration.Platform);
        Assert.True(registration.IsActive);

        await service.UnregisterAsync(firstUser, new(token));
        Assert.False((await db.DeviceRegistrations.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task ReRegistration_MovesPhysicalTokenToNewAuthenticatedAccount()
    {
        await using var db = CreateDb();
        var service = new DeviceRegistrationService(db);
        const string token = "fcm-token-abcdefghijklmnopqrstuvwxyz-0123456789";
        await service.RegisterAsync(Guid.NewGuid(), new(token, "android"));
        var newUser = Guid.NewGuid();

        await service.RegisterAsync(newUser, new(token, "android"));

        var registration = Assert.Single(db.DeviceRegistrations);
        Assert.Equal(newUser, registration.UserId);
        Assert.True(registration.IsActive);
    }

    [Fact]
    public async Task InvalidProviderToken_IsDisabled_AndPrivateFieldsNeverEnterPushPayload()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        const string token = "fcm-token-abcdefghijklmnopqrstuvwxyz-0123456789";
        var notification = new Notification
        {
            UserId = userId,
            Type = NotificationType.ClaimApproved,
            Title = "Claim update",
            Message = "Your claim status has changed.",
            RelatedEntityType = "Claim",
            RelatedEntityId = Guid.NewGuid()
        };
        db.DeviceRegistrations.Add(new DeviceRegistration { UserId = userId, FcmToken = token, Platform = "android" });
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        var push = new RecordingPush(PushDeliveryStatus.InvalidToken);
        var dispatcher = new NotificationPushDispatcher(db, push, NullLogger<NotificationPushDispatcher>.Instance);

        await dispatcher.DispatchAsync(notification);

        var request = Assert.Single(push.Requests);
        Assert.Equal("ClaimApproved", request.Type);
        Assert.Equal("Claim update", request.Title);
        Assert.Equal("Your claim status has changed.", request.Body);
        Assert.DoesNotContain("verification", request.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRET", request.Title + request.Body + request.Type + request.EntityId, StringComparison.OrdinalIgnoreCase);
        Assert.False((await db.DeviceRegistrations.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task ProviderFailure_DoesNotThrowOrDeleteThePersistedNotification()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var notification = new Notification { UserId = userId, Type = NotificationType.MessageReceived, Title = "Message", Message = "You have a new message." };
        db.DeviceRegistrations.Add(new DeviceRegistration { UserId = userId, FcmToken = "fcm-token-abcdefghijklmnopqrstuvwxyz-0123456789", Platform = "android" });
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        var dispatcher = new NotificationPushDispatcher(db, new ThrowingPush(), NullLogger<NotificationPushDispatcher>.Instance);

        await dispatcher.DispatchAsync(notification);

        Assert.NotNull(await db.Notifications.FindAsync(notification.Id));
    }

    [Theory]
    [InlineData(PushDeliveryStatus.RateLimited)]
    [InlineData(PushDeliveryStatus.Unavailable)]
    public async Task TransientProviderResults_KeepNotificationAndActiveRegistration(PushDeliveryStatus status)
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var notification = new Notification { UserId = userId, Type = NotificationType.ClaimRejected, Title = "Private title", Message = "Private content" };
        db.DeviceRegistrations.Add(new DeviceRegistration { UserId = userId, FcmToken = "fcm-token-abcdefghijklmnopqrstuvwxyz-0123456789", Platform = "android" });
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        var dispatcher = new NotificationPushDispatcher(db, new RecordingPush(status), NullLogger<NotificationPushDispatcher>.Instance);

        await dispatcher.DispatchAsync(notification);

        Assert.NotNull(await db.Notifications.FindAsync(notification.Id));
        Assert.True((await db.DeviceRegistrations.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task MissingFirebaseConfiguration_DoesNotAttemptDeliveryOrExposeCredentials()
    {
        var service = new FirebasePushNotificationService(
            Options.Create(new FirebaseOptions()),
            NullLogger<FirebasePushNotificationService>.Instance);

        var result = await service.SendAsync(new PushNotificationRequest(
            Guid.NewGuid(), Guid.NewGuid(), "fcm-token-abcdefghijklmnopqrstuvwxyz-0123456789",
            "Claim update", "Your claim status has changed.", "ClaimApproved", null));

        Assert.Equal(PushDeliveryStatus.ConfigurationError, result.Status);
    }

    private static FoundUDbContext CreateDb()
        => new(new DbContextOptionsBuilder<FoundUDbContext>()
            .UseInMemoryDatabase($"push-tests-{Guid.NewGuid():N}")
            .Options);

    private sealed class RecordingPush(PushDeliveryStatus result) : IPushNotificationService
    {
        public List<PushNotificationRequest> Requests { get; } = [];
        public Task<PushDeliveryResult> SendAsync(PushNotificationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new PushDeliveryResult(result));
        }
    }

    private sealed class ThrowingPush : IPushNotificationService
    {
        public Task<PushDeliveryResult> SendAsync(PushNotificationRequest request, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("provider unavailable");
    }
}
