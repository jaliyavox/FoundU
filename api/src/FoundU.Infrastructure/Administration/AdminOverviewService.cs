using FoundU.Application.Abstractions;
using FoundU.Application.Admin.Dtos;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Administration;

/// <summary>
/// The admin panel's front page: every queue that needs a person, and the last few things
/// that happened. Read-only - the levers live on the pages each number links to.
/// </summary>
public class AdminOverviewService : IAdminOverviewService
{
    private const int RecentCount = 12;

    private readonly FoundUDbContext _db;

    public AdminOverviewService(FoundUDbContext db)
    {
        _db = db;
    }

    public async Task<AdminOverviewDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var weekAgo = DateTime.UtcNow.AddDays(-7);

        var awaitingDecision = await _db.Claims.CountAsync(
            c => c.Status == ClaimStatus.Pending
                || c.Status == ClaimStatus.WaitingForAnswer
                || c.Status == ClaimStatus.UnderReview
                || c.Status == ClaimStatus.ManualReviewRequired,
            cancellationToken);

        // Approved but not collected: the item is reserved and somebody is expected at a desk.
        var approvedNotCollected = await _db.Claims.CountAsync(
            c => c.Status == ClaimStatus.Approved && c.CollectedAt == null, cancellationToken);

        var posted = await _db.FoundReports.CountAsync(
            r => r.Status == FoundReportStatus.Posted, cancellationToken);
        // Everything physically held, matching the items page's "In storage": an item waiting
        // for its owner's collection is still on the shelf.
        var unclaimed = await _db.FoundReports.CountAsync(
            r => r.Status == FoundReportStatus.Unclaimed || r.Status == FoundReportStatus.Claimed, cancellationToken);

        var flagged = await _db.LostReports.CountAsync(r => r.IsFlagged, cancellationToken);

        var supportOpen = await _db.SupportTickets.CountAsync(
            t => t.Status == SupportTicketStatus.Open, cancellationToken);
        var supportUnassigned = await _db.SupportTickets.CountAsync(
            t => t.Status == SupportTicketStatus.Open && t.AssignedToUserId == null, cancellationToken);
        var oldestSupport = await _db.SupportTickets
            .Where(t => t.Status == SupportTicketStatus.Open)
            .OrderBy(t => t.LastActivityAt)
            .Select(t => (DateTime?)t.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);

        var people = await _db.Users
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var suspended = await _db.Users.CountAsync(u => u.IsSuspended, cancellationToken);
        var newThisWeek = await _db.Users.CountAsync(u => u.CreatedAt >= weekAgo, cancellationToken);

        int RoleCount(UserRole role) => people.FirstOrDefault(p => p.Role == role)?.Count ?? 0;

        return new AdminOverviewDto(
            new AdminQueueDto(
                awaitingDecision,
                approvedNotCollected,
                posted,
                unclaimed,
                flagged,
                supportOpen,
                supportUnassigned,
                oldestSupport is null ? 0 : (int)Math.Max(0, (DateTime.UtcNow - oldestSupport.Value).TotalHours)),
            new AdminPeopleDto(
                RoleCount(UserRole.Student),
                RoleCount(UserRole.Staff),
                RoleCount(UserRole.Admin),
                suspended,
                newThisWeek),
            await RecentAsync(cancellationToken));
    }

    /// <summary>
    /// The last few things that happened, read from the records themselves rather than an
    /// audit table - nothing here is a second source of truth that could drift.
    /// </summary>
    private async Task<IReadOnlyList<AdminActivityDto>> RecentAsync(CancellationToken cancellationToken)
    {
        var reports = await _db.LostReports.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Take(RecentCount)
            .Select(r => new AdminActivityDto("LostReport", r.ItemType.Name + " reported lost at " + r.LastSeenLocation.Name, r.CreatedAt))
            .ToListAsync(cancellationToken);

        var items = await _db.FoundReports.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Take(RecentCount)
            .Select(r => new AdminActivityDto(
                r.FinderId == null ? "FoundReport" : "FoundPost",
                r.ItemType.Name + (r.FinderId == null ? " logged at a desk" : " posted by a finder"),
                r.CreatedAt))
            .ToListAsync(cancellationToken);

        var claims = await _db.Claims.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Take(RecentCount)
            .Select(c => new AdminActivityDto("Claim", "Claim on " + c.FoundReport.ItemType.Name, c.CreatedAt))
            .ToListAsync(cancellationToken);

        var tickets = await _db.SupportTickets.AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Take(RecentCount)
            .Select(t => new AdminActivityDto("SupportTicket", t.Subject, t.CreatedAt))
            .ToListAsync(cancellationToken);

        return reports
            .Concat(items)
            .Concat(claims)
            .Concat(tickets)
            .OrderByDescending(a => a.At)
            .Take(RecentCount)
            .ToList();
    }
}
