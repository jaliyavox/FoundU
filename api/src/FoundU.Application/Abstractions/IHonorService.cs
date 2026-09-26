using FoundU.Domain.Enums;

namespace FoundU.Application.Abstractions;

/// <summary>
/// Awards honor points for outcomes the platform can see. Like notifications, awards are
/// queued into the caller's unit of work: the service that decided the outcome is the one
/// that saves it, so points and the thing they were earned for land together or not at all.
/// </summary>
public interface IHonorService
{
    /// <summary>
    /// Queues an award unless this person already has one for this reason and this item.
    /// Returns false when it was a repeat, so callers can tell nothing new happened.
    /// </summary>
    Task<bool> QueueAwardAsync(
        Guid userId,
        HonorAwardReason reason,
        Guid? lostReportId,
        Guid? foundReportId,
        string detail,
        CancellationToken cancellationToken = default);
}
