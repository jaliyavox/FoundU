using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Administration;

public class AccountDeletionService : IAccountDeletionService
{
    private const string Reason = "The account was deleted by an administrator.";

    private readonly FoundUDbContext _db;
    private readonly ILostReportService _lostReports;
    private readonly IFoundPostService _foundPosts;
    private readonly IHandoverService _handovers;

    public AccountDeletionService(
        FoundUDbContext db,
        ILostReportService lostReports,
        IFoundPostService foundPosts,
        IHandoverService handovers)
    {
        _db = db;
        _lostReports = lostReports;
        _foundPosts = foundPosts;
        _handovers = handovers;
    }

    public async Task DeleteAsync(Guid userId, Guid actingAdminId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundAppException($"User '{userId}' was not found.");

        if (user.Id == actingAdminId)
            throw new ValidationAppException(nameof(userId), "You cannot delete your own account.");

        // Same rule as suspension: two administrators must not be able to remove each other.
        if (user.Role == UserRole.Admin)
            throw new ForbiddenAppException(
                "Administrator accounts cannot be deleted. Change their role to Staff first if this is intended.");

        // A claim decision must keep the name of the person who made it; hiding the account
        // would hide the decision from the claim's history too. Suspend them instead.
        if (await _db.ApprovalDecisions.AnyAsync(
                d => d.DecidedByUserId == userId || d.OverriddenByUserId == userId, cancellationToken))
            throw new ConflictAppException(
                "This person has decided claims, and those decisions must stay on record. Suspend the account instead.");

        // Checked before anything changes, so a refusal leaves the account exactly as it was.
        var itemWaiting = await _db.Claims.AnyAsync(
                c => c.StudentId == userId && c.Status == ClaimStatus.Approved && c.CollectedAt == null,
                cancellationToken)
            || await _db.LostReportFoundClaims.AnyAsync(
                h => h.LostReport.StudentId == userId && h.Status == HandoverStatus.InCustody,
                cancellationToken);
        if (itemWaiting)
            throw new ConflictAppException(
                "An item is waiting at the desk for this person. Hand it over or resolve it before deleting the account.");

        // Their open work, closed through the same paths they would use themselves, so claims,
        // handovers, history rows and notifications to other people all happen as usual.
        var walking = await _db.LostReportFoundClaims
            .Where(h => h.FinderId == userId && h.Status == HandoverStatus.AwaitingHandIn)
            .Select(h => h.LostReportId)
            .ToListAsync(cancellationToken);
        foreach (var reportId in walking)
            await _handovers.CancelAsync(reportId, userId, cancellationToken);

        var openReports = await _db.LostReports
            .Where(r => r.StudentId == userId
                && (r.Status == LostReportStatus.Active || r.Status == LostReportStatus.Matched))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        foreach (var reportId in openReports)
            await _lostReports.WithdrawAsync(reportId, userId, Reason, cancellationToken);

        // Posts still in the finder's hands come down; an item a desk already holds stays theirs.
        var posts = await _db.FoundReports
            .Where(p => p.FinderId == userId && p.Status == FoundReportStatus.Posted && p.HandedToSecurityAt == null)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        foreach (var postId in posts)
            await _foundPosts.WithdrawAsync(postId, userId, Reason, cancellationToken);

        foreach (var ticket in await _db.SupportTickets.Where(t => t.UserId == userId).ToListAsync(cancellationToken))
            _db.SupportTickets.Remove(ticket);

        foreach (var token in await _db.RefreshTokens
                     .Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(cancellationToken))
            token.RevokedAt = DateTime.UtcNow;

        foreach (var device in await _db.DeviceRegistrations
                     .Where(d => d.UserId == userId && d.IsActive).ToListAsync(cancellationToken))
            device.IsActive = false;

        // Personal details are erased, not kept behind a flag. The unique email, username,
        // student number and Google indexes still cover soft-deleted rows, so the person can
        // register again later with the same details.
        var tombstone = $"deleted-{user.Id:N}@deleted.foundu.invalid";
        user.FullName = "Deleted user";
        user.Email = tombstone;
        user.NormalizedEmail = tombstone.ToUpperInvariant();
        user.UserName = tombstone;
        user.NormalizedUserName = tombstone.ToUpperInvariant();
        user.EmailConfirmed = false;
        user.StudentNumber = null;
        user.GoogleSubjectId = null;
        user.PhoneNumber = null;
        user.PasswordHash = null;
        user.SecurityStamp = Guid.NewGuid().ToString();

        // A soft delete: the row stays for the history that points at it, and the global filter
        // hides it from every query, sign-in and token checks included. Set directly rather
        // than through Remove(), which would make EF sever the reports it is tracking.
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
