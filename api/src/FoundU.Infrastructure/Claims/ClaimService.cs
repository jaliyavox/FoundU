using FoundU.Application.Abstractions;
using FoundU.Application.Claims.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Claims;

public class ClaimService : IClaimService
{
    private readonly FoundUDbContext _db;

    public ClaimService(FoundUDbContext db)
    {
        _db = db;
    }

    public async Task<ClaimResponse> CreateAsync(Guid authenticatedUserId, CreateClaimRequest request)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => u.Id == authenticatedUserId);

        if (user is null)
        {
            throw new UnauthorizedAppException("The authenticated user no longer exists.");
        }

        if (user.Role != UserRole.Student)
        {
            throw new ForbiddenAppException("Only Students can create claims.");
        }

        if (user.IsSuspended)
        {
            throw new ForbiddenAppException("This account has been suspended. Contact an administrator.");
        }

        if (user.IsDeleted)
        {
            throw new ForbiddenAppException("This account is inactive.");
        }

        var lostReport = await _db.LostReports
            .SingleOrDefaultAsync(r => r.Id == request.LostReportId);

        if (lostReport is null)
        {
            throw new NotFoundAppException(nameof(LostReport), request.LostReportId);
        }

        if (lostReport.StudentId != authenticatedUserId)
        {
            throw new ForbiddenAppException("You can only create a claim using your own Lost Report.");
        }

        if (lostReport.Status is not (LostReportStatus.Active or LostReportStatus.Matched))
        {
            throw new ValidationAppException(
                nameof(request.LostReportId),
                "The Lost Report is not in a status that allows claiming.");
        }

        var foundReport = await _db.FoundReports
            .SingleOrDefaultAsync(r => r.Id == request.FoundReportId);

        if (foundReport is null)
        {
            throw new NotFoundAppException(nameof(FoundReport), request.FoundReportId);
        }

        if (foundReport.Status != FoundReportStatus.Unclaimed)
        {
            throw new ValidationAppException(
                nameof(request.FoundReportId),
                "The Found Report is no longer available for claiming.");
        }

        var foundReportAlreadyApproved = await _db.Claims.AnyAsync(c =>
            c.FoundReportId == request.FoundReportId &&
            c.Status == ClaimStatus.Approved);

        if (foundReportAlreadyApproved)
        {
            throw new ConflictAppException("The Found Report has already been successfully claimed.");
        }

        var duplicateActiveClaim = await _db.Claims.AnyAsync(c =>
            c.StudentId == authenticatedUserId &&
            c.LostReportId == request.LostReportId &&
            c.FoundReportId == request.FoundReportId &&
            c.Status != ClaimStatus.Rejected &&
            c.Status != ClaimStatus.Cancelled);

        if (duplicateActiveClaim)
        {
            throw new ConflictAppException("An active claim already exists for these Lost and Found Reports.");
        }

        var claim = new Claim
        {
            StudentId = authenticatedUserId,
            LostReportId = request.LostReportId,
            FoundReportId = request.FoundReportId,
            Status = ClaimStatus.Pending
        };

        _db.Claims.Add(claim);
        await _db.SaveChangesAsync();

        return new ClaimResponse(
            claim.Id,
            claim.LostReportId,
            claim.FoundReportId,
            claim.Status.ToString(),
            claim.CreatedAt);
    }
}
