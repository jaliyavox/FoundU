using FoundU.Application.Abstractions;
using FoundU.Application.Common;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Common.Pagination;
using FoundU.Application.FoundReports.Dtos;
using FoundU.Application.LostReports.Dtos;
using FoundU.Application.Matching.Dtos;
using FoundU.Domain.Common;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FoundU.Infrastructure.Reporting;

public class LostReportService : ILostReportService
{
    private readonly FoundUDbContext _db;
    private readonly IPhotoStorage _photoStorage;
    private readonly INotificationService _notifications;
    private readonly IDescriptionParserAgentClient _descriptionParser;

    public LostReportService(
        FoundUDbContext db,
        IPhotoStorage photoStorage,
        INotificationService notifications,
        IDescriptionParserAgentClient descriptionParser)
    {
        _db = db;
        _photoStorage = photoStorage;
        _notifications = notifications;
        _descriptionParser = descriptionParser;
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

        // The parser is optional enrichment only. Its client converts provider/configuration
        // failures to a safe result, leaving the student's normal report creation intact.
        var parse = await _descriptionParser.ParseAsync(
            request.Description.Trim(),
            $"description-parser-{Guid.NewGuid():N}",
            cancellationToken);

        var report = new LostReport
        {
            StudentId = studentId,
            HandInCode = await NextHandInCodeAsync(cancellationToken),
            CategoryId = request.CategoryId,
            ItemTypeId = request.ItemTypeId,
            LastSeenLocationId = request.LastSeenLocationId,
            Description = request.Description.Trim(),
            PrimaryColor = Normalize(request.PrimaryColor),
            SecondaryColor = Normalize(request.SecondaryColor),
            ParsedAttributesJson = parse.IsSuccess && parse.Value is not null
                ? SerializeParsedAttributes(parse.Value)
                : null,
            EstimatedLostFromAt = DateTime.SpecifyKind(request.EstimatedLostFromAt, DateTimeKind.Utc),
            EstimatedLostToAt = DateTime.SpecifyKind(request.EstimatedLostToAt, DateTimeKind.Utc),
            Status = LostReportStatus.Active,
        };

        _db.LostReports.Add(report);
        _db.AgentRuns.Add(CreateDescriptionParserRun(report.Id, parse));

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

    public async Task<LostReportDetailDto> UpdateAsync(
        Guid id,
        UpdateLostReportRequest request,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var report = await _db.LostReports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{id}' was not found.");

        if (report.StudentId != studentId)
        {
            throw new ForbiddenAppException("You can only edit your own lost reports.");
        }

        if (report.Status != LostReportStatus.Active)
        {
            throw new ConflictAppException("Only active reports can be edited.");
        }

        await EnsureItemTypeBelongsToCategoryAsync(request.CategoryId, request.ItemTypeId, cancellationToken);

        var locationExists = await _db.CampusLocations
            .AsNoTracking()
            .AnyAsync(l => l.Id == request.LastSeenLocationId, cancellationToken);

        if (!locationExists)
        {
            throw new NotFoundAppException($"Campus location '{request.LastSeenLocationId}' was not found.");
        }

        report.CategoryId = request.CategoryId;
        report.ItemTypeId = request.ItemTypeId;
        report.LastSeenLocationId = request.LastSeenLocationId;
        report.Description = request.Description.Trim();
        report.PrimaryColor = Normalize(request.PrimaryColor);
        report.SecondaryColor = Normalize(request.SecondaryColor);
        report.EstimatedLostFromAt = DateTime.SpecifyKind(request.EstimatedLostFromAt, DateTimeKind.Utc);
        report.EstimatedLostToAt = DateTime.SpecifyKind(request.EstimatedLostToAt, DateTimeKind.Utc);
        report.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadDetailAsync(report.Id, cancellationToken);
    }

    public async Task FlagAsync(
        Guid id,
        FlagLostReportRequest request,
        Guid userId,
        bool userIsStaff,
        CancellationToken cancellationToken = default)
    {
        var report = await _db.LostReports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{id}' was not found.");

        // The owner may flag their own report; staff may flag any. A flag asks for a person's
        // time, so it is not something one student gets to spend on another's report.
        if (!userIsStaff && report.StudentId != userId)
        {
            throw new ForbiddenAppException("You can only flag your own lost reports.");
        }

        if (report.IsFlagged)
        {
            throw new ConflictAppException("This report is already flagged and waiting for staff.");
        }

        report.IsFlagged = true;
        report.FlagReason = request.Reason.Trim();
        report.FlaggedAt = DateTime.UtcNow;
        report.FlaggedByUserId = userId;
        report.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearFlagAsync(
        Guid id,
        Guid staffId,
        CancellationToken cancellationToken = default)
    {
        var report = await _db.LostReports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{id}' was not found.");

        if (!report.IsFlagged)
        {
            throw new ConflictAppException("This report is not flagged.");
        }

        // The reason is kept: a report flagged twice is worth knowing about, and the audit of
        // who cleared it is the status history row.
        _db.LostReportStatusHistories.Add(new LostReportStatusHistory
        {
            LostReportId = report.Id,
            FromStatus = report.Status,
            ToStatus = report.Status,
            ChangedByUserId = staffId,
            Reason = $"Flag cleared. Was: {report.FlagReason}",
        });

        report.IsFlagged = false;
        report.FlaggedAt = null;
        report.FlaggedByUserId = null;
        report.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MatchSuggestionDto>> GetPossibleMatchesAsync(
        Guid reportId,
        Guid requesterId,
        bool requesterIsStaff,
        CancellationToken cancellationToken = default)
    {
        var report = await _db.LostReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{reportId}' was not found.");

        if (!requesterIsStaff && report.StudentId != requesterId)
        {
            throw new ForbiddenAppException("You can only view matches for your own lost reports.");
        }

        return await _db.MatchSuggestions
            .AsNoTracking()
            .Where(m => m.LostReportId == reportId && m.Status != MatchSuggestionStatus.Dismissed)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MatchSuggestionDto(
                m.Id,
                m.LostReportId,
                m.LostReport.Description,
                new FoundReportSummaryDto(
                    m.FoundReport.Id,
                    m.FoundReport.Category.Name,
                    m.FoundReport.ItemType.Name,
                    m.FoundReport.FoundLocation.Name,
                    m.FoundReport.GeneralDescription,
                    m.FoundReport.PrimaryColor,
                    m.FoundReport.FoundAt,
                    m.FoundReport.Status.ToString()),
                m.Status.ToString(),
                m.StaffNote,
                m.GeneratedByAgentRunId != null,
                m.GeneratedByAgentRunId == null ? null : (decimal?)m.MatchScore,
                m.LostReport.Claims
                    .Where(c => c.FoundReportId == m.FoundReportId)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => (Guid?)c.Id)
                    .FirstOrDefault(),
                m.CreatedAt))
            .ToListAsync(cancellationToken);
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
        Guid? requesterId = null,
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
                r.HandInCode,
                r.Student.FullName,
                requesterId != null && r.StudentId == requesterId,
                r.Category.Name,
                r.ItemType.Name,
                r.LastSeenLocation.Name,
                r.Description,
                r.PrimaryColor,
                r.EstimatedLostFromAt,
                r.EstimatedLostToAt,
                r.Photos.Select(p => p.Url).ToList(),
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

    public async Task<IReadOnlyList<LostReportPhotoDto>> AddPhotosAsync(
        Guid reportId,
        Guid ownerId,
        IReadOnlyList<PhotoUpload> uploads,
        CancellationToken cancellationToken = default)
    {
        var report = await _db.LostReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{reportId}' was not found.");

        if (report.StudentId != ownerId)
        {
            throw new ForbiddenAppException("You can only add photos to your own reports.");
        }

        if (uploads.Count == 0)
        {
            throw new ValidationAppException("photos", "Choose at least one image.");
        }

        var existing = await _db.LostItemPhotos.CountAsync(p => p.LostReportId == reportId, cancellationToken);

        if (existing + uploads.Count > PhotoRules.MaxPhotosPerReport)
        {
            throw new ValidationAppException("photos",
                $"A report can have at most {PhotoRules.MaxPhotosPerReport} photos. This one already has {existing}.");
        }

        var saved = new List<LostItemPhoto>();

        foreach (var upload in uploads)
        {
            if (upload.Length > PhotoRules.MaxBytes)
            {
                throw new ValidationAppException("photos",
                    $"'{upload.FileName}' is larger than {PhotoRules.MaxSizeLabel}.");
            }

            // Read the header and check what the file actually is. The declared content type
            // and the extension both come from the client, so neither can be trusted.
            var header = new byte[12];
            var read = await upload.Content.ReadAsync(header, cancellationToken);
            upload.Content.Position = 0;

            var extension = PhotoRules.ResolveExtension(header.AsSpan(0, read));

            if (extension is null)
            {
                throw new ValidationAppException("photos",
                    $"'{upload.FileName}' is not a JPEG, PNG or WebP image.");
            }

            var url = await _photoStorage.SaveAsync(
                upload with { FileName = extension },
                $"uploads/lost-reports/{reportId:N}",
                cancellationToken);

            saved.Add(new LostItemPhoto { LostReportId = reportId, Url = url });
        }

        _db.LostItemPhotos.AddRange(saved);
        await _db.SaveChangesAsync(cancellationToken);

        return saved.Select(p => new LostReportPhotoDto(p.Id, p.Url)).ToList();
    }

    public async Task<LostReportFoundClaimDto> RegisterFoundClaimAsync(
        Guid reportId,
        Guid finderId,
        CancellationToken cancellationToken = default)
    {
        var report = await _db.LostReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{reportId}' was not found.");

        if (report.StudentId == finderId)
        {
            throw new ForbiddenAppException("This is your own report - you cannot report finding it.");
        }

        if (report.Status != LostReportStatus.Active)
        {
            throw new ConflictAppException("This report is closed and is no longer looking for the item.");
        }

        var existing = await _db.LostReportFoundClaims
            .FirstOrDefaultAsync(c => c.LostReportId == reportId && c.FinderId == finderId, cancellationToken);

        // Pressing it again is the same claim, not a second finder. Returning the original
        // keeps the button safe to press twice without inflating what the author sees.
        if (existing is null)
        {
            existing = new LostReportFoundClaim { LostReportId = reportId, FinderId = finderId };
            _db.LostReportFoundClaims.Add(existing);

            // Only on the first press. Pressing again is the same claim, and telling the
            // author twice would make one finder look like two.
            _notifications.Queue(
                report.StudentId,
                NotificationType.ItemReportedFound,
                "Someone says they found your item",
                "They have been asked to hand it in at a desk. You will hear where it went if they say.",
                nameof(LostReport),
                reportId);

            await _db.SaveChangesAsync(cancellationToken);
        }

        var totalFinders = await _db.LostReportFoundClaims
            .CountAsync(c => c.LostReportId == reportId, cancellationToken);

        return new LostReportFoundClaimDto(reportId, totalFinders, existing.CreatedAt);
    }

    public async Task<LostReportMessageDto> SendMessageAsync(
        Guid reportId,
        Guid senderId,
        string body,
        Guid? recipientId,
        CancellationToken cancellationToken = default)
    {
        var report = await _db.LostReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{reportId}' was not found.");

        // A withdrawn or resolved report is no longer looking for anything.
        if (report.Status is LostReportStatus.Withdrawn or LostReportStatus.Resolved)
        {
            throw new ConflictAppException("This report is closed and is no longer accepting messages.");
        }

        var isAuthor = report.StudentId == senderId;
        Guid recipient;

        if (isAuthor)
        {
            // The author replies into an existing thread - never opens one. A finder who has
            // not written cannot be written to, which keeps the direction of first contact
            // with the person who has the item.
            recipient = recipientId
                ?? throw new ValidationAppException(nameof(SendLostReportMessageRequest.RecipientId), "Say who the reply is to.");

            var threadExists = await _db.LostReportMessages.AnyAsync(
                m => m.LostReportId == reportId && m.SenderId == recipient, cancellationToken);
            if (!threadExists)
            {
                throw new ValidationAppException(nameof(SendLostReportMessageRequest.RecipientId), "That person has not written to you about this report.");
            }
        }
        else
        {
            if (recipientId is not null && recipientId != report.StudentId)
            {
                throw new ValidationAppException(nameof(SendLostReportMessageRequest.RecipientId), "A finder can only write to the report's author.");
            }
            recipient = report.StudentId;
        }

        var message = new LostReportMessage
        {
            LostReportId = reportId,
            SenderId = senderId,
            RecipientId = recipient,
            Body = body.Trim(),
        };

        _db.LostReportMessages.Add(message);

        _notifications.Queue(
            recipient,
            NotificationType.MessageReceived,
            isAuthor ? "A reply about the item you found" : "A message about your lost item",
            // The body is quoted rather than summarised: "you have a new message" makes
            // someone open the app to find out something they could have been told.
            message.Body.Length <= 140 ? message.Body : message.Body[..140] + "...",
            nameof(LostReport),
            reportId);

        await _db.SaveChangesAsync(cancellationToken);

        var names = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == senderId || u.Id == recipient)
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync(cancellationToken);

        return new LostReportMessageDto(
            message.Id,
            names.First(n => n.Id == senderId).FullName,
            true,
            recipient,
            names.First(n => n.Id == recipient).FullName,
            message.Body,
            message.IsRead,
            message.CreatedAt);
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

        var isAuthor = report.StudentId == requesterId;

        var messages = _db.LostReportMessages
            .AsNoTracking()
            .Where(m => m.LostReportId == reportId);

        // The author reads every thread; staff read all to settle a dispute; a finder reads
        // only the thread they are in. Anyone else has nothing here - 403, because the
        // report itself is public and its existence is no secret.
        if (!isAuthor && !requesterIsStaff)
        {
            var participates = await messages.AnyAsync(
                m => m.SenderId == requesterId || m.RecipientId == requesterId, cancellationToken);
            if (!participates)
            {
                throw new ForbiddenAppException("You can only read messages you are part of.");
            }
            messages = messages.Where(m => m.SenderId == requesterId || m.RecipientId == requesterId);
        }

        var authorId = report.StudentId;

        return await messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new LostReportMessageDto(
                m.Id,
                m.Sender.FullName,
                m.SenderId == requesterId,
                // The counterpart is whoever is not the author: the finder that thread belongs to.
                m.SenderId == authorId ? (m.RecipientId ?? authorId) : m.SenderId,
                m.SenderId == authorId
                    ? (m.Recipient == null ? m.Sender.FullName : m.Recipient.FullName)
                    : m.Sender.FullName,
                m.Body,
                m.IsRead,
                m.CreatedAt))
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
        if (query.Flagged is { } flagged) reports = reports.Where(r => r.IsFlagged == flagged);

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
                r.HandInCode,
                r.Category.Name,
                r.ItemType.Name,
                r.LastSeenLocation.Name,
                r.Description,
                r.PrimaryColor,
                r.EstimatedLostFromAt,
                r.EstimatedLostToAt,
                r.Status.ToString(),
                r.Photos.Select(p => p.Url).ToList(),
                r.Messages.Count(),
                r.FoundClaims.Count(),
                r.FoundClaims
                    .Max(c => (DateTime?)c.CreatedAt),
                r.IsFlagged,
                r.FlagReason,
                r.FlaggedAt,
                r.FlaggedByUser == null ? null : r.FlaggedByUser.FullName,
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
                r.HandInCode,
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
                r.ParsedAttributesJson,
                r.Photos.Select(p => new LostReportPhotoDto(p.Id, p.Url)).ToList(),
                r.IsFlagged,
                r.FlagReason,
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

    /// <summary>
    /// A million codes and a few hundred reports: a collision is rare, but the unique index
    /// makes it a crash rather than a duplicate, so it is checked here first.
    /// </summary>
    private async Task<string> NextHandInCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = HandoverCodes.Generate();
            if (!await _db.LostReports.AnyAsync(r => r.HandInCode == code, cancellationToken)) return code;
        }

        throw new InvalidOperationException("Could not allocate a unique hand-in code.");
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

    private static string SerializeParsedAttributes(DescriptionParserAgentResult parse)
        => JsonSerializer.Serialize(new
        {
            itemType = parse.ItemType,
            primaryColor = parse.PrimaryColor,
            secondaryColor = parse.SecondaryColor,
            identifyingFeatures = parse.IdentifyingFeatures,
            confidenceScore = parse.ConfidenceScore,
        });

    private static AgentRun CreateDescriptionParserRun(
        Guid reportId,
        DescriptionParserAgentCallResult<DescriptionParserAgentResult> parse)
        => new()
        {
            TriggerEntityType = nameof(LostReport),
            TriggerEntityId = reportId,
            Objective = "Enrich a lost report with parsed attributes.",
            PlanJson = JsonSerializer.Serialize(new { steps = new[] { "parse_description", "validate_attributes" } }),
            Status = parse.IsSuccess ? AgentRunStatus.Completed : AgentRunStatus.Failed,
            RetryCount = parse.RetryCount,
            ErrorMessage = parse.IsSuccess ? null : "Description parser was unavailable.",
            // Never duplicate student description text or raw provider content in the audit.
            FinalOutcomeJson = parse.IsSuccess && parse.Value is not null
                ? JsonSerializer.Serialize(new
                {
                    agent = "description_parser",
                    remoteAgentRunId = parse.Value.AgentRunId,
                    outcome = "enriched",
                    confidence = parse.Value.ConfidenceScore,
                })
                : JsonSerializer.Serialize(new { agent = "description_parser", outcome = "fallback" }),
            CompletedAt = DateTime.UtcNow,
        };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
