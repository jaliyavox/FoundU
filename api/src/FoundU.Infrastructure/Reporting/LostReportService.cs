using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Common.Pagination;
using FoundU.Application.LostReports.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Reporting;

public class LostReportService : ILostReportService
{
    private readonly FoundUDbContext _db;

    public LostReportService(FoundUDbContext db)
    {
        _db = db;
    }

    public async Task<LostReportDetailDto> CreateAsync(
        CreateLostReportRequest request,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureItemTypeBelongsToCategoryAsync(request.CategoryId, request.ItemTypeId, cancellationToken);

        var locationExists = await _db.CampusLocations
            .AsNoTracking()
            .AnyAsync(l => l.Id == request.LastSeenLocationId, cancellationToken);

        if (!locationExists)
        {
            throw new NotFoundAppException($"Campus location '{request.LastSeenLocationId}' was not found.");
        }

        var report = new LostReport
        {
            StudentId = studentId,
            CategoryId = request.CategoryId,
            ItemTypeId = request.ItemTypeId,
            LastSeenLocationId = request.LastSeenLocationId,
            Description = request.Description.Trim(),
            PrimaryColor = Normalize(request.PrimaryColor),
            SecondaryColor = Normalize(request.SecondaryColor),
            EstimatedLostFromAt = DateTime.SpecifyKind(request.EstimatedLostFromAt, DateTimeKind.Utc),
            EstimatedLostToAt = DateTime.SpecifyKind(request.EstimatedLostToAt, DateTimeKind.Utc),
            Status = LostReportStatus.Active,
        };

        _db.LostReports.Add(report);

        _db.LostReportStatusHistories.Add(new LostReportStatusHistory
        {
            LostReportId = report.Id,
            FromStatus = LostReportStatus.Active,
            ToStatus = LostReportStatus.Active,
            ChangedByUserId = studentId,
            Reason = "Report submitted",
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadDetailAsync(report.Id, cancellationToken);
    }

    public Task<PagedResult<LostReportListItemDto>> SearchAsync(
        LostReportQuery query,
        CancellationToken cancellationToken = default)
        => SearchCoreAsync(_db.LostReports.AsNoTracking(), query, cancellationToken);

    public Task<PagedResult<LostReportListItemDto>> SearchForStudentAsync(
        Guid studentId,
        LostReportQuery query,
        CancellationToken cancellationToken = default)
        => SearchCoreAsync(_db.LostReports.AsNoTracking().Where(r => r.StudentId == studentId), query, cancellationToken);

    public async Task<PagedResult<LostReportFeedItemDto>> GetPublicFeedAsync(
        LostReportQuery query,
        CancellationToken cancellationToken = default)
    {
        // Active only: a withdrawn or resolved report is no longer something to look out for.
        var reports = _db.LostReports
            .AsNoTracking()
            .Where(r => r.Status == LostReportStatus.Active);

        if (query.CategoryId is { } categoryId) reports = reports.Where(r => r.CategoryId == categoryId);
        if (query.ItemTypeId is { } itemTypeId) reports = reports.Where(r => r.ItemTypeId == itemTypeId);
        if (query.LastSeenLocationId is { } locationId) reports = reports.Where(r => r.LastSeenLocationId == locationId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";

            reports = reports.Where(r =>
                EF.Functions.ILike(r.Description, term) ||
                (r.PrimaryColor != null && EF.Functions.ILike(r.PrimaryColor, term)) ||
                EF.Functions.ILike(r.ItemType.Name, term) ||
                EF.Functions.ILike(r.Category.Name, term) ||
                EF.Functions.ILike(r.LastSeenLocation.Name, term));
        }

        // Newest first, always - a feed has one sensible order and no caller-supplied sort.
        reports = reports.OrderByDescending(r => r.CreatedAt);

        var totalCount = await reports.CountAsync(cancellationToken);

        var items = await reports
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new LostReportFeedItemDto(
                r.Id,
                r.Student.FullName,
                r.Category.Name,
                r.ItemType.Name,
                r.LastSeenLocation.Name,
                r.Description,
                r.PrimaryColor,
                r.EstimatedLostFromAt,
                r.EstimatedLostToAt,
                r.CreatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<LostReportFeedItemDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<LostReportDetailDto> GetByIdAsync(
        Guid id,
        Guid requesterId,
        bool requesterIsStaff,
        CancellationToken cancellationToken = default)
    {
        var report = await LoadDetailAsync(id, cancellationToken);

        // A student may only read their own report. Staff and Admin see every report.
        if (!requesterIsStaff && report.StudentId != requesterId)
        {
            throw new ForbiddenAppException("You can only view your own lost reports.");
        }

        return report;
    }

    public async Task<LostReportDetailDto> WithdrawAsync(
        Guid id,
        Guid studentId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var report = await _db.LostReports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{id}' was not found.");

        if (report.StudentId != studentId)
        {
            throw new ForbiddenAppException("You can only withdraw your own lost reports.");
        }

        if (report.Status == LostReportStatus.Withdrawn)
        {
            throw new ConflictAppException("This report has already been withdrawn.");
        }

        if (report.Status == LostReportStatus.Resolved)
        {
            throw new ConflictAppException("A resolved report cannot be withdrawn.");
        }

        var previousStatus = report.Status;

        report.Status = LostReportStatus.Withdrawn;
        report.WithdrawReason = Normalize(reason);
        report.WithdrawnAt = DateTime.UtcNow;
        report.UpdatedAt = DateTime.UtcNow;

        _db.LostReportStatusHistories.Add(new LostReportStatusHistory
        {
            LostReportId = report.Id,
            FromStatus = previousStatus,
            ToStatus = LostReportStatus.Withdrawn,
            ChangedByUserId = studentId,
            Reason = report.WithdrawReason ?? "Withdrawn by student",
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadDetailAsync(report.Id, cancellationToken);
    }

    public async Task<LostReportMessageDto> SendMessageAsync(
        Guid reportId,
        Guid senderId,
        string body,
        CancellationToken cancellationToken = default)
    {
        var report = await _db.LostReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{reportId}' was not found.");

        if (report.StudentId == senderId)
        {
            throw new ValidationAppException(nameof(LostReportMessage.Body),
                "This is your own report - you cannot message yourself.");
        }

        // A withdrawn or resolved report is no longer looking for anything.
        if (report.Status != LostReportStatus.Active)
        {
            throw new ConflictAppException("This report is closed and is no longer accepting messages.");
        }

        var message = new LostReportMessage
        {
            LostReportId = reportId,
            SenderId = senderId,
            Body = body.Trim(),
        };

        _db.LostReportMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        var senderName = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == senderId)
            .Select(u => u.FullName)
            .FirstAsync(cancellationToken);

        return new LostReportMessageDto(message.Id, senderName, message.Body, message.IsRead, message.CreatedAt);
    }

    public async Task<IReadOnlyList<LostReportMessageDto>> GetMessagesAsync(
        Guid reportId,
        Guid requesterId,
        bool requesterIsStaff,
        CancellationToken cancellationToken = default)
    {
        var report = await _db.LostReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{reportId}' was not found.");

        // Only the author reads their own messages. Staff may read them to settle a dispute.
        if (!requesterIsStaff && report.StudentId != requesterId)
        {
            throw new ForbiddenAppException("You can only read messages on your own reports.");
        }

        return await _db.LostReportMessages
            .AsNoTracking()
            .Where(m => m.LostReportId == reportId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new LostReportMessageDto(m.Id, m.Sender.FullName, m.Body, m.IsRead, m.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<PagedResult<LostReportListItemDto>> SearchCoreAsync(
        IQueryable<LostReport> reports,
        LostReportQuery query,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<LostReportStatus>(query.Status, ignoreCase: true, out var status))
            {
                throw new ValidationAppException(nameof(query.Status),
                    $"Unknown status '{query.Status}'. Expected one of: {string.Join(", ", Enum.GetNames<LostReportStatus>())}.");
            }

            reports = reports.Where(r => r.Status == status);
        }

        if (query.CategoryId is { } categoryId) reports = reports.Where(r => r.CategoryId == categoryId);
        if (query.ItemTypeId is { } itemTypeId) reports = reports.Where(r => r.ItemTypeId == itemTypeId);
        if (query.LastSeenLocationId is { } locationId) reports = reports.Where(r => r.LastSeenLocationId == locationId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";

            reports = reports.Where(r =>
                EF.Functions.ILike(r.Description, term) ||
                (r.PrimaryColor != null && EF.Functions.ILike(r.PrimaryColor, term)) ||
                EF.Functions.ILike(r.ItemType.Name, term) ||
                EF.Functions.ILike(r.Category.Name, term));
        }

        reports = ApplySort(reports, query);

        var totalCount = await reports.CountAsync(cancellationToken);

        var items = await reports
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new LostReportListItemDto(
                r.Id,
                r.Category.Name,
                r.ItemType.Name,
                r.LastSeenLocation.Name,
                r.Description,
                r.PrimaryColor,
                r.EstimatedLostFromAt,
                r.EstimatedLostToAt,
                r.Status.ToString(),
                r.CreatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<LostReportListItemDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    private async Task<LostReportDetailDto> LoadDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var report = await _db.LostReports
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new LostReportDetailDto(
                r.Id,
                r.CategoryId,
                r.Category.Name,
                r.ItemTypeId,
                r.ItemType.Name,
                r.LastSeenLocationId,
                r.LastSeenLocation.Name,
                r.Description,
                r.PrimaryColor,
                r.SecondaryColor,
                r.EstimatedLostFromAt,
                r.EstimatedLostToAt,
                r.Status.ToString(),
                r.WithdrawReason,
                r.WithdrawnAt,
                r.StudentId,
                r.Student.FullName,
                r.CreatedAt,
                r.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return report ?? throw new NotFoundAppException($"Lost report '{id}' was not found.");
    }

    /// <summary>Allow-listed sort columns only - never interpolate a caller-supplied column name.</summary>
    private static IQueryable<LostReport> ApplySort(IQueryable<LostReport> reports, LostReportQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return query.SortBy?.ToLowerInvariant() switch
        {
            "lostfrom" => descending ? reports.OrderByDescending(r => r.EstimatedLostFromAt) : reports.OrderBy(r => r.EstimatedLostFromAt),
            "status" => descending ? reports.OrderByDescending(r => r.Status) : reports.OrderBy(r => r.Status),
            "category" => descending ? reports.OrderByDescending(r => r.Category.Name) : reports.OrderBy(r => r.Category.Name),
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
            throw new ValidationAppException(nameof(CreateLostReportRequest.ItemTypeId),
                "The selected item type does not belong to the selected category.");
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
