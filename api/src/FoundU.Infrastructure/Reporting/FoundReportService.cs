using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Common.Pagination;
using FoundU.Application.FoundReports.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Reporting;

public class FoundReportService : IFoundReportService
{
    private readonly FoundUDbContext _db;

    public FoundReportService(FoundUDbContext db)
    {
        _db = db;
    }

    public async Task<FoundReportDetailDto> CreateAsync(
        CreateFoundReportRequest request,
        Guid staffId,
        CancellationToken cancellationToken = default)
    {
        // Validate the foreign keys ourselves so a bad id returns a clear 404 rather than a
        // raw FK violation surfacing as a 500.
        await EnsureItemTypeBelongsToCategoryAsync(request.CategoryId, request.ItemTypeId, cancellationToken);
        await EnsureExistsAsync(_db.CampusLocations, request.FoundLocationId, "Campus location", cancellationToken);
        await EnsureExistsAsync(_db.StorageLocations, request.StorageLocationId, "Storage location", cancellationToken);

        var report = new FoundReport
        {
            StaffId = staffId,
            CategoryId = request.CategoryId,
            ItemTypeId = request.ItemTypeId,
            FoundLocationId = request.FoundLocationId,
            StorageLocationId = request.StorageLocationId,
            GeneralDescription = request.GeneralDescription.Trim(),
            PrivateVerificationDetails = string.IsNullOrWhiteSpace(request.PrivateVerificationDetails)
                ? null
                : request.PrivateVerificationDetails.Trim(),
            PrimaryColor = Normalize(request.PrimaryColor),
            SecondaryColor = Normalize(request.SecondaryColor),
            FoundAt = DateTime.SpecifyKind(request.FoundAt, DateTimeKind.Utc),
            Status = FoundReportStatus.Unclaimed,
        };

        _db.FoundReports.Add(report);

        // Seed the audit trail with the opening state, so every later transition has a predecessor.
        _db.FoundReportStatusHistories.Add(new FoundReportStatusHistory
        {
            FoundReportId = report.Id,
            FromStatus = FoundReportStatus.Unclaimed,
            ToStatus = FoundReportStatus.Unclaimed,
            ChangedByUserId = staffId,
            Reason = "Item logged",
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(report.Id, cancellationToken);
    }

    public async Task<PagedResult<FoundReportListItemDto>> SearchAsync(
        FoundReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var reports = _db.FoundReports.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<FoundReportStatus>(query.Status, ignoreCase: true, out var status))
            {
                throw new ValidationAppException(nameof(query.Status),
                    $"Unknown status '{query.Status}'. Expected one of: {string.Join(", ", Enum.GetNames<FoundReportStatus>())}.");
            }

            reports = reports.Where(r => r.Status == status);
        }

        if (query.CategoryId is { } categoryId) reports = reports.Where(r => r.CategoryId == categoryId);
        if (query.ItemTypeId is { } itemTypeId) reports = reports.Where(r => r.ItemTypeId == itemTypeId);
        if (query.FoundLocationId is { } locationId) reports = reports.Where(r => r.FoundLocationId == locationId);
        if (query.FoundFrom is { } from) reports = reports.Where(r => r.FoundAt >= from);
        if (query.FoundTo is { } to) reports = reports.Where(r => r.FoundAt <= to);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";

            // Deliberately does NOT search PrivateVerificationDetails: a search box that matched
            // hidden evidence would let someone probe for it by trial and error.
            reports = reports.Where(r =>
                EF.Functions.ILike(r.GeneralDescription, term) ||
                (r.PrimaryColor != null && EF.Functions.ILike(r.PrimaryColor, term)) ||
                EF.Functions.ILike(r.ItemType.Name, term) ||
                EF.Functions.ILike(r.Category.Name, term));
        }

        reports = ApplySort(reports, query);

        var totalCount = await reports.CountAsync(cancellationToken);

        var items = await reports
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new FoundReportListItemDto(
                r.Id,
                r.Category.Name,
                r.ItemType.Name,
                r.FoundLocation.Name,
                r.StorageLocation.Name,
                r.GeneralDescription,
                r.PrimaryColor,
                r.FoundAt,
                r.Status.ToString(),
                r.PrivateVerificationDetails != null,
                r.CreatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<FoundReportListItemDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<FoundReportDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await _db.FoundReports
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new FoundReportDetailDto(
                r.Id,
                r.CategoryId,
                r.Category.Name,
                r.ItemTypeId,
                r.ItemType.Name,
                r.FoundLocationId,
                r.FoundLocation.Name,
                r.StorageLocationId,
                r.StorageLocation.Name,
                r.GeneralDescription,
                r.PrivateVerificationDetails,
                r.PrimaryColor,
                r.SecondaryColor,
                r.FoundAt,
                r.Status.ToString(),
                r.StaffId,
                r.Staff.FullName,
                r.CreatedAt,
                r.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return report ?? throw new NotFoundAppException($"Found report '{id}' was not found.");
    }

    /// <summary>Allow-listed sort columns only - never interpolate a caller-supplied column name.</summary>
    private static IQueryable<FoundReport> ApplySort(IQueryable<FoundReport> reports, FoundReportQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return query.SortBy?.ToLowerInvariant() switch
        {
            "foundat" => descending ? reports.OrderByDescending(r => r.FoundAt) : reports.OrderBy(r => r.FoundAt),
            "status" => descending ? reports.OrderByDescending(r => r.Status) : reports.OrderBy(r => r.Status),
            "category" => descending ? reports.OrderByDescending(r => r.Category.Name) : reports.OrderBy(r => r.Category.Name),
            // Newest first is the useful default for a lost-and-found desk.
            _ => descending ? reports.OrderByDescending(r => r.CreatedAt) : reports.OrderBy(r => r.CreatedAt),
        };
    }

    private async Task EnsureItemTypeBelongsToCategoryAsync(Guid categoryId, Guid itemTypeId, CancellationToken cancellationToken)
    {
        var itemType = await _db.ItemTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == itemTypeId, cancellationToken)
            ?? throw new NotFoundAppException($"Item type '{itemTypeId}' was not found.");

        if (itemType.CategoryId != categoryId)
        {
            throw new ValidationAppException(nameof(CreateFoundReportRequest.ItemTypeId),
                "The selected item type does not belong to the selected category.");
        }
    }

    private static async Task EnsureExistsAsync<TEntity>(
        IQueryable<TEntity> set,
        Guid id,
        string label,
        CancellationToken cancellationToken)
        where TEntity : Domain.Common.BaseEntity
    {
        var exists = await set.AsNoTracking().AnyAsync(e => e.Id == id, cancellationToken);

        if (!exists)
        {
            throw new NotFoundAppException($"{label} '{id}' was not found.");
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
