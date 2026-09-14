using FoundU.Application.Abstractions;
using FoundU.Application.Admin.Dtos;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Administration;

/// <summary>
/// The admin overview. Grouped queries rather than loading tables into memory - the numbers
/// stay cheap as the tables grow, and each one is a count the database did, not one we
/// arrived at.
/// </summary>
public class AdminAnalyticsService : IAdminAnalyticsService
{
    private const int Days = 30;

    private readonly FoundUDbContext _db;

    public AdminAnalyticsService(FoundUDbContext db)
    {
        _db = db;
    }

    public async Task<AnalyticsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var lostByStatus = await _db.LostReports
            .GroupBy(r => r.Status)
            .Select(g => new StatusCountDto(g.Key.ToString(), g.Count()))
            .ToListAsync(cancellationToken);

        var foundByStatus = await _db.FoundReports
            .GroupBy(f => f.Status)
            .Select(g => new StatusCountDto(g.Key.ToString(), g.Count()))
            .ToListAsync(cancellationToken);

        var claimsByStatus = await _db.Claims
            .GroupBy(c => c.Status)
            .Select(g => new StatusCountDto(g.Key.ToString(), g.Count()))
            .ToListAsync(cancellationToken);

        /* ------------------------------------------------------ daily activity */

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var since = today.AddDays(-(Days - 1)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var lostPerDay = await _db.LostReports
            .Where(r => r.CreatedAt >= since)
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => DateOnly.FromDateTime(x.Key), x => x.Count, cancellationToken);

        var foundPerDay = await _db.FoundReports
            .Where(f => f.CreatedAt >= since)
            .GroupBy(f => f.CreatedAt.Date)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => DateOnly.FromDateTime(x.Key), x => x.Count, cancellationToken);

        // "Returned" is the day a claim was approved - the decision row is the record of it.
        var returnedPerDay = await _db.ApprovalDecisions
            .Where(d => d.Decision == ApprovalDecisionType.Approved && d.DecidedAt >= since)
            .GroupBy(d => d.DecidedAt.Date)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => DateOnly.FromDateTime(x.Key), x => x.Count, cancellationToken);

        // Zero-filled: a day with nothing is a real data point, and a chart with gaps in its
        // x-axis reads as missing data rather than a quiet day.
        var daily = Enumerable.Range(0, Days)
            .Select(offset => today.AddDays(-(Days - 1) + offset))
            .Select(day => new DailyActivityDto(
                day,
                lostPerDay.GetValueOrDefault(day),
                foundPerDay.GetValueOrDefault(day),
                returnedPerDay.GetValueOrDefault(day)))
            .ToList();

        /* --------------------------------------------------------- categories */

        var lostByCategory = await _db.LostReports
            .GroupBy(r => r.Category.Name)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var foundByCategory = await _db.FoundReports
            .GroupBy(f => f.Category.Name)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var topCategories = lostByCategory.Select(x => x.Category)
            .Union(foundByCategory.Select(x => x.Category))
            .Select(name => new CategoryActivityDto(
                name,
                lostByCategory.FirstOrDefault(x => x.Category == name)?.Count ?? 0,
                foundByCategory.FirstOrDefault(x => x.Category == name)?.Count ?? 0))
            .OrderByDescending(c => c.Lost + c.Found)
            .ThenBy(c => c.Category)
            .Take(5)
            .ToList();

        /* ------------------------------------------------------ time to return */

        // Posted -> approved, per approved claim. The two timestamps come back and the
        // subtraction happens here: approvals number in the tens for a campus desk, and the
        // date-diff functions differ per database. A mean, and the label says so.
        var daysToReturn = (await _db.ApprovalDecisions
                .Where(d => d.Decision == ApprovalDecisionType.Approved)
                .Select(d => new { Posted = d.Claim.LostReport.CreatedAt, Approved = d.DecidedAt })
                .ToListAsync(cancellationToken))
            .Select(x => (x.Approved - x.Posted).TotalDays)
            .ToList();

        var resolved = await _db.LostReports.CountAsync(r => r.Status == LostReportStatus.Resolved, cancellationToken);
        var openFlags = await _db.LostReports.CountAsync(r => r.IsFlagged, cancellationToken);

        return new AnalyticsOverviewDto(
            lostByStatus,
            foundByStatus,
            claimsByStatus,
            daily,
            topCategories,
            daysToReturn.Count == 0 ? null : Math.Round(daysToReturn.Average(), 1),
            resolved,
            openFlags,
            DateTime.UtcNow);
    }
}
