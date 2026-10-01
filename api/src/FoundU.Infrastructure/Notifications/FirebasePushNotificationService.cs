using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using FoundU.Application.Abstractions;
using FoundU.Application.Notifications.Dtos;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FoundU.Infrastructure.Notifications;

/// <summary>
/// Delivers the minimal FoundU push payload through Firebase Admin. Configuration failures are
/// returned as results so notification-producing business transactions never fail because FCM is
/// absent or unavailable.
/// </summary>
public class FirebasePushNotificationService : IPushNotificationService
{
    private static readonly object FirebaseAppLock = new();
    private static FirebaseApp? _app;
    private readonly FirebaseOptions _options;
    private readonly ILogger<FirebasePushNotificationService> _logger;

    public FirebasePushNotificationService(
        IOptions<FirebaseOptions> options,
        ILogger<FirebasePushNotificationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PushDeliveryResult> SendAsync(PushNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var messaging = GetMessaging();
        if (messaging is null)
        {
            _logger.LogWarning("Push failed because Firebase is not configured. NotificationId: {NotificationId}, UserId: {UserId}",
                request.NotificationId, request.UserId);
            return new(PushDeliveryStatus.ConfigurationError);
        }

        var data = new Dictionary<string, string>
        {
            ["type"] = request.Type,
            ["notificationId"] = request.NotificationId.ToString()
        };
        if (!string.IsNullOrWhiteSpace(request.EntityId))
            data["entityId"] = request.EntityId;

        var message = new Message
        {
            Token = request.DeviceToken,
            Notification = new FirebaseAdmin.Messaging.Notification { Title = request.Title, Body = request.Body },
            Data = data
        };

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 15)));
            await messaging.SendAsync(message).WaitAsync(timeout.Token);
            _logger.LogInformation("Push succeeded. NotificationId: {NotificationId}, UserId: {UserId}", request.NotificationId, request.UserId);
            return new(PushDeliveryStatus.Delivered);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Push timed out. NotificationId: {NotificationId}, UserId: {UserId}", request.NotificationId, request.UserId);
            return new(PushDeliveryStatus.Unavailable);
        }
        catch (FirebaseMessagingException ex)
        {
            var code = ex.MessagingErrorCode.ToString();
            var status = code is "Unregistered" or "InvalidArgument" ? PushDeliveryStatus.InvalidToken
                : code is "QuotaExceeded" ? PushDeliveryStatus.RateLimited
                : code is "Unavailable" or "Internal" ? PushDeliveryStatus.Unavailable
                : PushDeliveryStatus.Failed;
            _logger.LogWarning("Push provider returned {PushStatus}. NotificationId: {NotificationId}, UserId: {UserId}",
                status, request.NotificationId, request.UserId);
            return new(status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push provider failed. NotificationId: {NotificationId}, UserId: {UserId}", request.NotificationId, request.UserId);
            return new(PushDeliveryStatus.Failed);
        }
    }

    private FirebaseMessaging? GetMessaging()
    {
        if (string.IsNullOrWhiteSpace(_options.CredentialsPath) || !File.Exists(_options.CredentialsPath))
            return null;

        try
        {
            lock (FirebaseAppLock)
            {
                _app ??= FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromFile(_options.CredentialsPath),
                    ProjectId = _options.ProjectId
                }, "foundu-fcm");
                return FirebaseMessaging.GetMessaging(_app);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Firebase initialization failed without exposing credential details.");
            return null;
        }
    }
}
