using FoundU.Application.Common.Exceptions;
using FoundU.Application.Desk;
using FoundU.Domain.Common;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Handovers;

/// <summary>
/// One box at the desk for every code a finder might quote. Staff should not have to know
/// which of three screens a code belongs to before they can type it.
///
/// Read-only, and it says nothing the desk did not already type: no other code, no hidden
/// verification detail. Each page it points at does its own checks before anything changes.
/// </summary>
public sealed class DeskCodeService(FoundUDbContext db) : IDeskCodeService
{
    public async Task<IReadOnlyList<DeskCodeMatch>> ResolveAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalised = code.Replace(" ", "");
        if (!HandoverCodes.LooksValid(normalised))
            throw new ValidationAppException("code", "A finder's code is six digits.");

        var matches = new List<DeskCodeMatch>();

        // Their own post, still waiting for a desk.
        matches.AddRange(await db.FoundReports.AsNoTracking()
            .Where(f => f.HandInCode == normalised && f.Status == FoundReportStatus.Posted)
            .Select(f => new DeskCodeMatch(
                "found-post",
                f.Id,
                (f.PrimaryColor != null ? f.PrimaryColor + " " : "") + f.ItemType.Name,
                "Found post by " + (f.Finder == null ? "a student" : f.Finder.FullName) + " · found at " + f.FoundLocation.Name,
                null, null, null))
            .ToListAsync(cancellationToken));

        // A walk to security started from someone's lost report, on its way or at a desk. An
        // expired walk is over - the handover page treats it as unknown, and so does this.
        var now = DateTime.UtcNow;
        matches.AddRange(await db.LostReportFoundClaims.AsNoTracking()
            .Where(h => h.HandoverCode == normalised
                && ((h.Status == HandoverStatus.AwaitingHandIn
                        && (h.HandoverExpiresAt == null || h.HandoverExpiresAt >= now))
                    || h.Status == HandoverStatus.InCustody))
            .Select(h => new DeskCodeMatch(
                "handover",
                h.LostReportId,
                (h.LostReport.PrimaryColor != null ? h.LostReport.PrimaryColor + " " : "") + h.LostReport.ItemType.Name,
                "Handover from " + h.Finder.FullName + " for " + h.LostReport.Student.FullName + "'s report · " + h.Status.ToString(),
                null, null, null))
            .ToListAsync(cancellationToken));

        // The code printed on an open lost report: the finder spotted it and walked in.
        matches.AddRange(await db.LostReports.AsNoTracking()
            .Where(r => r.HandInCode == normalised
                && (r.Status == LostReportStatus.Active || r.Status == LostReportStatus.Matched))
            .Select(r => new DeskCodeMatch(
                "lost-report",
                r.Id,
                (r.PrimaryColor != null ? r.PrimaryColor + " " : "") + r.ItemType.Name,
                "Reported lost by " + r.Student.FullName + " · last seen at " + r.LastSeenLocation.Name,
                r.CategoryId,
                r.ItemTypeId,
                r.PrimaryColor))
            .ToListAsync(cancellationToken));

        return matches.Count > 0
            ? matches
            : throw new NotFoundAppException("Nothing open has that code. Check it with the finder - or log the item without one.");
    }
}
