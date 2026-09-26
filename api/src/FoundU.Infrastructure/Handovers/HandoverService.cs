using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Handovers.Dtos;
using FoundU.Domain.Common;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Handovers;

/// <summary>
/// A finder walking somebody's lost item to a desk, and the owner collecting it there.
///
/// One code, minted when the finder commits to the walk and known only to those two people.
/// The desk types it in twice: once to take the item, once to release it. Because a code says
/// which item and never who the person is, releasing it requires the desk to confirm it
/// checked the collector's ID, and who did so is recorded.
///
/// While the walk is on, the report comes off the feed - nobody else should set out after
/// something already on its way home - and the pause lapses by itself if nobody turns up.
/// </summary>
public class HandoverService : IHandoverService
{
    /// <summary>Long enough to cross campus and sleep on it; short enough that a no-show is not for ever.</summary>
    private static readonly TimeSpan HandoverWindow = TimeSpan.FromHours(48);

    private readonly FoundUDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IHonorService _honor;

    public HandoverService(FoundUDbContext db, INotificationService notifications, IHonorService honor)
    {
        _db = db;
        _notifications = notifications;
        _honor = honor;
    }

    public async Task<HandoverDto> StartAsync(Guid reportId, Guid finderId, CancellationToken cancellationToken = default)
    {
        var report = await _db.LostReports
            .Include(r => r.ItemType)
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{reportId}' was not found.");

        if (report.StudentId == finderId)
            throw new ForbiddenAppException("This is your own report.");

        if (report.Status is not (LostReportStatus.Active or LostReportStatus.Matched))
            throw new ConflictAppException("This report is closed - there is nothing to hand in.");

        await ExpireLapsedAsync(reportId, cancellationToken);

        // Somebody else already has it in motion. Two people cannot both be carrying it, and
        // saying so plainly is better than minting a second code the desk cannot tell apart.
        var otherOpen = await _db.LostReportFoundClaims.AnyAsync(
            c => c.LostReportId == reportId
                && c.FinderId != finderId
                && (c.Status == HandoverStatus.AwaitingHandIn || c.Status == HandoverStatus.InCustody),
            cancellationToken);
        if (otherOpen)
            throw new ConflictAppException("Someone else is already handing this item in. Message the owner if you think you have theirs.");

        var claim = await _db.LostReportFoundClaims
            .FirstOrDefaultAsync(c => c.LostReportId == reportId && c.FinderId == finderId, cancellationToken);

        if (claim is null)
        {
            claim = new LostReportFoundClaim { LostReportId = reportId, FinderId = finderId };
            _db.LostReportFoundClaims.Add(claim);
        }

        // Pressing it again is the same walk, not a second one: keep the code they are holding.
        if (claim.Status is HandoverStatus.AwaitingHandIn or HandoverStatus.InCustody && claim.HandoverCode is not null)
            return await LoadAsync(claim.Id, finderId, cancellationToken);

        if (claim.Status == HandoverStatus.Collected)
            throw new ConflictAppException("This item has already gone home.");

        var now = DateTime.UtcNow;
        claim.Status = HandoverStatus.AwaitingHandIn;
        claim.HandoverCode = await NextCodeAsync(cancellationToken);
        claim.HandoverStartedAt = now;
        claim.HandoverExpiresAt = now + HandoverWindow;
        claim.UpdatedAt = now;

        report.PausedUntil = claim.HandoverExpiresAt;
        report.UpdatedAt = now;

        var finderName = await _db.Users.Where(u => u.Id == finderId).Select(u => u.FullName).FirstAsync(cancellationToken);
        var itemName = report.ItemType.Name.ToLowerInvariant();

        _notifications.Queue(
            report.StudentId,
            NotificationType.HandoverStarted,
            $"Your {itemName} is on its way to a desk",
            $"{FirstName(finderName)} is taking it in. Quote {HandoverCodes.Display(claim.HandoverCode)} at the desk with your student ID to collect it.",
            nameof(LostReport),
            reportId);

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadAsync(claim.Id, finderId, cancellationToken);
    }

    public async Task<HandoverDto> CancelAsync(Guid reportId, Guid finderId, CancellationToken cancellationToken = default)
    {
        var claim = await _db.LostReportFoundClaims
            .FirstOrDefaultAsync(c => c.LostReportId == reportId && c.FinderId == finderId, cancellationToken)
            ?? throw new NotFoundAppException("You have no handover on this report.");

        if (claim.Status == HandoverStatus.InCustody)
            throw new ConflictAppException("A desk already has this item - it is out of your hands now.");

        if (claim.Status != HandoverStatus.AwaitingHandIn)
            throw new ConflictAppException("There is nothing to cancel.");

        claim.Status = HandoverStatus.Cancelled;
        claim.HandoverCode = null;
        claim.HandoverExpiresAt = null;
        claim.UpdatedAt = DateTime.UtcNow;

        await UnpauseIfNothingOpenAsync(reportId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        // The owner was told it was coming; they should hear that it is not.
        var itemName = await _db.LostReports
            .Where(r => r.Id == reportId)
            .Select(r => r.ItemType.Name)
            .FirstAsync(cancellationToken);
        var ownerId = await _db.LostReports.Where(r => r.Id == reportId).Select(r => r.StudentId).FirstAsync(cancellationToken);

        _notifications.Queue(
            ownerId,
            NotificationType.HandoverCancelled,
            $"That {itemName.ToLowerInvariant()} is not coming after all",
            "The finder cancelled. Your report is back on the feed.",
            nameof(LostReport),
            reportId);

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadAsync(claim.Id, finderId, cancellationToken);
    }

    public async Task<HandoverDto?> GetForUserAsync(Guid reportId, Guid userId, CancellationToken cancellationToken = default)
    {
        await ExpireLapsedAsync(reportId, cancellationToken);

        var report = await _db.LostReports.AsNoTracking()
            .Where(r => r.Id == reportId)
            .Select(r => new { r.StudentId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{reportId}' was not found.");

        // The owner sees whichever handover is live on their report; a finder sees their own.
        var claim = report.StudentId == userId
            ? await _db.LostReportFoundClaims.AsNoTracking()
                .Where(c => c.LostReportId == reportId && (c.Status == HandoverStatus.AwaitingHandIn || c.Status == HandoverStatus.InCustody))
                .OrderByDescending(c => c.HandoverStartedAt)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync(cancellationToken)
            : await _db.LostReportFoundClaims.AsNoTracking()
                .Where(c => c.LostReportId == reportId && c.FinderId == userId)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync(cancellationToken);

        return claim is null ? null : await LoadAsync(claim.Id, userId, cancellationToken);
    }

    public async Task<HandoverLookupDto> LookupAsync(string code, CancellationToken cancellationToken = default)
        => ToLookup(await FindByCodeAsync(code, cancellationToken));

    public async Task<HandoverLookupDto> ReceiveAsync(
        string code,
        Guid staffId,
        ReceiveHandoverRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = await FindByCodeAsync(code, cancellationToken, tracking: true);

        if (claim.Status != HandoverStatus.AwaitingHandIn)
            throw new ConflictAppException("This code is not waiting for a hand-in.");

        var storage = await _db.StorageLocations
            .FirstOrDefaultAsync(s => s.Id == request.StorageLocationId, cancellationToken)
            ?? throw new ValidationAppException(nameof(ReceiveHandoverRequest.StorageLocationId), "That storage location does not exist.");

        var report = claim.LostReport;
        var now = DateTime.UtcNow;

        // The item becomes a logged found item like any other, so the rest of the system -
        // storage, audits, analytics - sees it the way it sees everything else in custody.
        var item = new FoundReport
        {
            FinderId = claim.FinderId,
            StaffId = staffId,
            CategoryId = report.CategoryId,
            ItemTypeId = report.ItemTypeId,
            FoundLocationId = report.LastSeenLocationId,
            StorageLocationId = storage.Id,
            GeneralDescription = report.Description.Length > 500 ? report.Description[..500] : report.Description,
            PrimaryColor = report.PrimaryColor,
            FoundAt = claim.HandoverStartedAt ?? now,
            Status = FoundReportStatus.Claimed,
        };
        _db.FoundReports.Add(item);

        claim.Status = HandoverStatus.InCustody;
        claim.HandedInAt = now;
        claim.ReceivedByStaffId = staffId;
        claim.FoundReport = item;
        claim.UpdatedAt = now;

        // The walk is over, so the clock stops: the item is on a shelf, not in a bag.
        claim.HandoverExpiresAt = null;
        report.PausedUntil = null;
        report.Status = LostReportStatus.Matched;
        report.UpdatedAt = now;

        _db.LostReportStatusHistories.Add(new LostReportStatusHistory
        {
            LostReportId = report.Id,
            FromStatus = LostReportStatus.Active,
            ToStatus = LostReportStatus.Matched,
            ChangedByUserId = staffId,
            Reason = "Handed in by the finder and logged at a desk.",
        });

        _notifications.Queue(
            report.StudentId,
            NotificationType.CollectionInstructions,
            "Your item is at the desk",
            $"It is at {storage.Name}. Bring your student ID and quote {HandoverCodes.Display(claim.HandoverCode!)}.",
            nameof(LostReport),
            report.Id);

        _notifications.Queue(
            claim.FinderId,
            NotificationType.FoundPostConfirmed,
            "Thank you - the desk has it",
            $"It is logged at {storage.Name}. Its owner has been told where to collect it.",
            nameof(LostReport),
            report.Id);

        await _honor.QueueAwardAsync(
            claim.FinderId,
            HonorAwardReason.HandedInAtDesk,
            report.Id,
            null,
            $"Handed in a {report.ItemType.Name.ToLowerInvariant()} at {storage.Name}",
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return ToLookup(await FindByCodeAsync(code, cancellationToken));
    }

    public async Task<HandoverLookupDto> ReleaseAsync(
        string code,
        Guid staffId,
        ReleaseHandoverRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = await FindByCodeAsync(code, cancellationToken, tracking: true);

        if (claim.Status != HandoverStatus.InCustody)
            throw new ConflictAppException("This code has no item waiting at a desk.");

        if (!request.OwnerIdChecked)
            throw new ValidationAppException(nameof(ReleaseHandoverRequest.OwnerIdChecked), "Check the collector's student ID against the owner's name first.");

        var report = claim.LostReport;
        var now = DateTime.UtcNow;

        claim.Status = HandoverStatus.Collected;
        claim.CollectedAt = now;
        claim.CollectedByStaffId = staffId;
        claim.CollectionCheck = string.IsNullOrWhiteSpace(request.Note)
            ? "Student ID checked against the owner's name."
            : request.Note.Trim();
        // Once. The code dies with the handover it belonged to.
        claim.HandoverCode = null;
        claim.UpdatedAt = now;

        if (claim.FoundReport is { } item)
        {
            _db.FoundReportStatusHistories.Add(new FoundReportStatusHistory
            {
                FoundReportId = item.Id,
                FromStatus = item.Status,
                ToStatus = FoundReportStatus.Returned,
                ChangedByUserId = staffId,
                Reason = "Collected by the owner with their handover code.",
            });
            item.Status = FoundReportStatus.Returned;
            item.UpdatedAt = now;
        }

        if (report.Status != LostReportStatus.Resolved)
        {
            _db.LostReportStatusHistories.Add(new LostReportStatusHistory
            {
                LostReportId = report.Id,
                FromStatus = report.Status,
                ToStatus = LostReportStatus.Resolved,
                ChangedByUserId = staffId,
                Reason = "Collected from the desk.",
            });
            report.Status = LostReportStatus.Resolved;
        }
        report.PausedUntil = null;
        report.UpdatedAt = now;

        _notifications.Queue(
            claim.FinderId,
            NotificationType.ItemReturnedToOwner,
            "It got home",
            $"The {report.ItemType.Name.ToLowerInvariant()} you handed in has been collected by its owner. Thank you.",
            nameof(LostReport),
            report.Id);

        await _honor.QueueAwardAsync(
            claim.FinderId,
            HonorAwardReason.HelpedReturn,
            report.Id,
            null,
            $"Helped return a {report.ItemType.Name.ToLowerInvariant()}",
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return new HandoverLookupDto(
            report.Id,
            claim.Status.ToString(),
            report.ItemType.Name,
            report.Category.Name,
            report.Description,
            report.PrimaryColor,
            report.LastSeenLocation.Name,
            report.Student.FullName,
            report.Student.StudentNumber,
            claim.Finder.FullName,
            claim.HandoverStartedAt ?? now,
            null,
            claim.HandedInAt,
            claim.FoundReport?.StorageLocation?.Name,
            "done");
    }

    /* ------------------------------------------------------------------ internals */

    private async Task<LostReportFoundClaim> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken,
        bool tracking = false)
    {
        var normalised = code.Replace(" ", "");

        var query = _db.LostReportFoundClaims
            .Include(c => c.Finder)
            .Include(c => c.LostReport).ThenInclude(r => r.Student)
            .Include(c => c.LostReport).ThenInclude(r => r.ItemType)
            .Include(c => c.LostReport).ThenInclude(r => r.Category)
            .Include(c => c.LostReport).ThenInclude(r => r.LastSeenLocation)
            .Include(c => c.FoundReport).ThenInclude(i => i!.StorageLocation)
            .AsQueryable();

        if (!tracking) query = query.AsNoTracking();

        // Not found rather than forbidden: a wrong code must not confirm that a right one
        // exists, and a spent code looks exactly like one that never did.
        var claim = await query.FirstOrDefaultAsync(c => c.HandoverCode == normalised, cancellationToken)
            ?? throw new NotFoundAppException("No handover has that code.");

        if (claim.Status == HandoverStatus.AwaitingHandIn
            && claim.HandoverExpiresAt is { } expires
            && expires < DateTime.UtcNow)
        {
            throw new NotFoundAppException("No handover has that code.");
        }

        return claim;
    }

    /// <summary>
    /// Lapsed walks are cleaned up where they are noticed rather than by a background job:
    /// one fewer moving part, and the only places it matters are the ones that read them.
    /// </summary>
    private async Task ExpireLapsedAsync(Guid reportId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var lapsed = await _db.LostReportFoundClaims
            .Where(c => c.LostReportId == reportId
                && c.Status == HandoverStatus.AwaitingHandIn
                && c.HandoverExpiresAt != null
                && c.HandoverExpiresAt < now)
            .ToListAsync(cancellationToken);

        if (lapsed.Count == 0) return;

        foreach (var claim in lapsed)
        {
            claim.Status = HandoverStatus.Expired;
            claim.HandoverCode = null;
            claim.UpdatedAt = now;
        }

        await UnpauseIfNothingOpenAsync(reportId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task UnpauseIfNothingOpenAsync(Guid reportId, CancellationToken cancellationToken)
    {
        // Loaded and then judged in memory, deliberately. This runs while the claim that was
        // just cancelled or expired is still only changed in the change tracker, and a
        // COUNT against the database would happily report it as open and leave the notice
        // paused for ever. Tracked entities come back with their current values.
        var claims = await _db.LostReportFoundClaims
            .Where(c => c.LostReportId == reportId)
            .ToListAsync(cancellationToken);

        if (claims.Any(c => c.IsHandoverOpen)) return;

        var report = await _db.LostReports.FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);
        if (report is null) return;

        report.PausedUntil = null;
        report.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<string> NextCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = HandoverCodes.Generate();
            var taken = await _db.LostReportFoundClaims.AnyAsync(c => c.HandoverCode == code, cancellationToken);
            if (!taken) return code;
        }

        throw new ConflictAppException("Could not allocate a handover code. Try again.");
    }

    private async Task<HandoverDto> LoadAsync(Guid claimId, Guid readerId, CancellationToken cancellationToken)
    {
        var claim = await _db.LostReportFoundClaims.AsNoTracking()
            .Include(c => c.Finder)
            .Include(c => c.LostReport)
            .Include(c => c.FoundReport).ThenInclude(i => i!.StorageLocation)
            .FirstAsync(c => c.Id == claimId, cancellationToken);

        // Only the two people involved ever see the code.
        var maySeeCode = readerId == claim.FinderId || readerId == claim.LostReport.StudentId;

        return new HandoverDto(
            claim.LostReportId,
            claim.Status.ToString(),
            maySeeCode ? claim.HandoverCode : null,
            claim.HandoverStartedAt,
            claim.HandoverExpiresAt,
            claim.HandedInAt,
            claim.CollectedAt,
            claim.FoundReport?.StorageLocation?.Name,
            claim.Finder.FullName);
    }

    private static HandoverLookupDto ToLookup(LostReportFoundClaim claim) => new(
        claim.LostReportId,
        claim.Status.ToString(),
        claim.LostReport.ItemType.Name,
        claim.LostReport.Category.Name,
        claim.LostReport.Description,
        claim.LostReport.PrimaryColor,
        claim.LostReport.LastSeenLocation.Name,
        claim.LostReport.Student.FullName,
        claim.LostReport.Student.StudentNumber,
        claim.Finder.FullName,
        claim.HandoverStartedAt ?? claim.CreatedAt,
        claim.HandoverExpiresAt,
        claim.HandedInAt,
        claim.FoundReport?.StorageLocation?.Name,
        claim.Status == HandoverStatus.AwaitingHandIn ? "receive"
            : claim.Status == HandoverStatus.InCustody ? "release"
            : "done");

    private static string FirstName(string fullName)
        => fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? fullName;
}
