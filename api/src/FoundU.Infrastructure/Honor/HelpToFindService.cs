using FoundU.Application.Abstractions;
using FoundU.Application.Honor.Dtos;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Honor;

/// <summary>
/// Read-only view of one person's finding activity. It is also where a finder goes back to
/// re-read the six digits they need at the desk, which is why the hand-in code is carried
/// here for items that have not reached one yet.
/// </summary>
public class HelpToFindService : IHelpToFindService
{
    private const int MaxActivity = 50;

    private readonly FoundUDbContext _db;

    public HelpToFindService(FoundUDbContext db)
    {
        _db = db;
    }

    public async Task<HelpToFindDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var points = await _db.HonorAwards
            .Where(a => a.UserId == userId)
            .SumAsync(a => (int?)a.Points, cancellationToken) ?? 0;

        var awards = await _db.HonorAwards.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => new { a.Reason, a.Points, a.LostReportId, a.FoundReportId })
            .ToListAsync(cancellationToken);

        // "I found this" on someone's report. The owner's code is public on the feed, so the
        // finder may keep reading it here until the item is somewhere else.
        var offers = await _db.LostReportFoundClaims.AsNoTracking()
            .Where(c => c.FinderId == userId && !c.LostReport.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Take(MaxActivity)
            .Select(c => new
            {
                c.LostReportId,
                c.LostReport.ItemType.Name,
                Location = c.LostReport.LastSeenLocation.Name,
                c.LostReport.Status,
                c.LostReport.HandInCode,
                OwnerName = c.LostReport.Student.FullName,
                c.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        // Things they picked up and posted themselves.
        var posts = await _db.FoundReports.AsNoTracking()
            .Where(r => r.FinderId == userId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .Take(MaxActivity)
            .Select(r => new
            {
                r.Id,
                r.ItemType.Name,
                Location = r.FoundLocation.Name,
                r.Status,
                r.HandInCode,
                r.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var activity = offers
            .Select(o => new HelpToFindActivityDto(
                "found-claim",
                o.LostReportId,
                o.Name,
                o.Location,
                o.Status.ToString(),
                o.Status == LostReportStatus.Active || o.Status == LostReportStatus.Matched ? o.HandInCode : null,
                FirstName(o.OwnerName),
                awards.Where(a => a.LostReportId == o.LostReportId).Sum(a => a.Points),
                o.CreatedAt))
            .Concat(posts.Select(p => new HelpToFindActivityDto(
                "found-post",
                p.Id,
                p.Name,
                p.Location,
                p.Status.ToString(),
                p.Status == FoundReportStatus.Posted ? p.HandInCode : null,
                null,
                awards.Where(a => a.FoundReportId == p.Id).Sum(a => a.Points),
                p.CreatedAt)))
            .OrderByDescending(a => a.CreatedAt)
            .Take(MaxActivity)
            .ToList();

        return new HelpToFindDto(
            points,
            awards.Count(a => a.Reason == HonorAwardReason.HelpedReturn),
            awards.Count(a => a.Reason == HonorAwardReason.HandedInAtDesk),
            activity.Count(a => a.HandInCode is not null),
            activity);
    }

    /// <summary>The author is a stranger to the finder - a first name is enough to talk about.</summary>
    private static string FirstName(string fullName)
        => fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? fullName;
}
