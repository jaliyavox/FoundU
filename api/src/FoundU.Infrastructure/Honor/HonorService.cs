using FoundU.Application.Abstractions;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Honor;

public class HonorService : IHonorService
{
    /// <summary>What each outcome is worth. One place, so the numbers can be argued about once.</summary>
    public static int PointsFor(HonorAwardReason reason) => reason switch
    {
        HonorAwardReason.HandedInAtDesk => 10,
        HonorAwardReason.HelpedReturn => 25,
        _ => 0,
    };

    private readonly FoundUDbContext _db;

    public HonorService(FoundUDbContext db)
    {
        _db = db;
    }

    public async Task<bool> QueueAwardAsync(
        Guid userId,
        HonorAwardReason reason,
        Guid? lostReportId,
        Guid? foundReportId,
        string detail,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || (lostReportId is null && foundReportId is null))
            return false;

        // Both the saved rows and the ones already queued in this unit of work count, so a
        // single request cannot award the same outcome twice before anything is saved.
        var alreadySaved = await _db.HonorAwards.AnyAsync(
            a => a.UserId == userId
                && a.Reason == reason
                && a.LostReportId == lostReportId
                && a.FoundReportId == foundReportId,
            cancellationToken);
        if (alreadySaved) return false;

        var alreadyQueued = _db.ChangeTracker.Entries<HonorAward>().Any(entry =>
            entry.State == EntityState.Added
            && entry.Entity.UserId == userId
            && entry.Entity.Reason == reason
            && entry.Entity.LostReportId == lostReportId
            && entry.Entity.FoundReportId == foundReportId);
        if (alreadyQueued) return false;

        _db.HonorAwards.Add(new HonorAward
        {
            UserId = userId,
            Reason = reason,
            Points = PointsFor(reason),
            LostReportId = lostReportId,
            FoundReportId = foundReportId,
            Detail = detail.Length > 200 ? detail[..200] : detail,
        });
        return true;
    }
}
