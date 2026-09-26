using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FoundU.Infrastructure.Notifications;

/// <summary>Captures newly persisted internal notifications and delivers push only after commit.</summary>
public class NotificationPushSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly NotificationPushDispatcher _dispatcher;
    private readonly ILogger<NotificationPushSaveChangesInterceptor> _logger;
    private readonly List<Notification> _pending = [];

    public NotificationPushSaveChangesInterceptor(
        NotificationPushDispatcher dispatcher,
        ILogger<NotificationPushSaveChangesInterceptor> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var pending = _pending.ToArray();
        _pending.Clear();
        foreach (var notification in pending)
            await DispatchSafelyAsync(notification, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        // Synchronous application paths are rare, but should never throw while dispatching push.
        var pending = _pending.ToArray();
        _pending.Clear();
        foreach (var notification in pending)
            DispatchSafelyAsync(notification).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        // Nothing committed, so a later successful save must not deliver a stale notification.
        _pending.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
            return;
        _pending.AddRange(context.ChangeTracker.Entries<Notification>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity));
    }

    private async Task DispatchSafelyAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dispatcher.DispatchAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            // The database transaction has already succeeded. Do not turn a best-effort provider
            // problem into an API failure or suggest that the business action was rolled back.
            _logger.LogWarning(ex, "Post-commit push dispatch failed safely. NotificationId: {NotificationId}", notification.Id);
        }
    }
}
