using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Common.Pagination;
using FoundU.Application.FoundReports.Dtos;
using FoundU.Application.Matching.Dtos;
using FoundU.Domain.Common;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FoundU.Infrastructure.Reporting;

public class FoundPostService : IFoundPostService
{
    /// <summary>
    /// How many open lost reports the matcher is asked about per post. Bounded because each
    /// is a call to the AI service; the candidates are the newest in the same category.
    /// </summary>
    private const int MatchCandidates = 5;

    private readonly FoundUDbContext _db;
    private readonly IMatchSuggestionService _suggestions;
    private readonly IFoundReportService _foundReports;
    private readonly INotificationService _notifications;
    private readonly IHonorService _honor;
    private readonly ILogger<FoundPostService> _logger;

    public FoundPostService(
        FoundUDbContext db,
        IMatchSuggestionService suggestions,
        IFoundReportService foundReports,
        INotificationService notifications,
        IHonorService honor,
        ILogger<FoundPostService> logger)
    {
        _db = db;
        _suggestions = suggestions;
        _foundReports = foundReports;
        _notifications = notifications;
        _honor = honor;
        _logger = logger;
    }

    public async Task<FoundPostFeedItemDto> PostAsync(
        CreateFoundPostRequest request,
        Guid finderId,
        CancellationToken cancellationToken = default)
    {
        var itemType = await _db.ItemTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.ItemTypeId, cancellationToken)
            ?? throw new NotFoundAppException($"Item type '{request.ItemTypeId}' was not found.");

        if (itemType.CategoryId != request.CategoryId)
        {
            throw new ValidationAppException(nameof(request.ItemTypeId), "That item type is not in the chosen category.");
        }

        if (!await _db.CampusLocations.AnyAsync(l => l.Id == request.FoundLocationId, cancellationToken))
        {
            throw new NotFoundAppException($"Campus location '{request.FoundLocationId}' was not found.");
        }

        // If the finder already spotted the owner's post, resolve its code before writing
        // anything, so a typo is a 400 and not a post with a dangling intent.
        Guid? linkedLostReportId = null;
        if (!string.IsNullOrWhiteSpace(request.LostReportHandInCode))
        {
            var code = request.LostReportHandInCode.Replace(" ", "");
            linkedLostReportId = await _db.LostReports
                .Where(r => r.HandInCode == code
                    && (r.Status == LostReportStatus.Active || r.Status == LostReportStatus.Matched))
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ValidationAppException(nameof(request.LostReportHandInCode), "No open report has that code.");
        }

        var post = new FoundReport
        {
            FinderId = finderId,
            HandInCode = await NextHandInCodeAsync(cancellationToken),
            CategoryId = request.CategoryId,
            ItemTypeId = request.ItemTypeId,
            FoundLocationId = request.FoundLocationId,
            GeneralDescription = request.Description.Trim(),
            PrimaryColor = string.IsNullOrWhiteSpace(request.PrimaryColor) ? null : request.PrimaryColor.Trim(),
            FoundAt = DateTime.SpecifyKind(request.FoundAt, DateTimeKind.Utc),
            Status = FoundReportStatus.Posted,
        };

        _db.FoundReports.Add(post);
        _db.FoundReportStatusHistories.Add(new FoundReportStatusHistory
        {
            FoundReportId = post.Id,
            FromStatus = FoundReportStatus.Posted,
            ToStatus = FoundReportStatus.Posted,
            ChangedByUserId = finderId,
            Reason = "Posted by the finder.",
        });

        await _db.SaveChangesAsync(cancellationToken);

        if (linkedLostReportId is { } lostReportId)
        {
            await _suggestions.CreateAsync(
                new CreateMatchSuggestionRequest(lostReportId, post.Id, "The finder matched it to your post themselves."),
                finderId,
                cancellationToken);
        }
        else
        {
            await MatchAgainstOpenReportsAsync(post, finderId, cancellationToken);
        }

        return await LoadAsync(post.Id, finderId, cancellationToken);
    }

    /// <summary>
    /// The matching agent, bounded, against the newest open reports in the same category.
    /// Best effort: the post stands on the feed whether or not the agent answers, so any
    /// failure here is logged and swallowed rather than surfaced to the finder.
    /// </summary>
    private async Task MatchAgainstOpenReportsAsync(FoundReport post, Guid finderId, CancellationToken cancellationToken)
    {
        // A fixed budget for all candidates together. The finder is waiting on this request,
        // and if the AI service is down, five timeouts in a row is minutes on a spinner for
        // something that changes nothing about whether their post stands.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(20));

        var candidates = await _db.LostReports
            .AsNoTracking()
            .Where(r => r.Status == LostReportStatus.Active && r.CategoryId == post.CategoryId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(MatchCandidates)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        foreach (var lostReportId in candidates)
        {
            if (budget.IsCancellationRequested) break;
            try
            {
                await _suggestions.GenerateWithAgentAsync(
                    new CreateMatchSuggestionRequest(lostReportId, post.Id, "A finder posted this - not at a desk yet."),
                    finderId,
                    budget.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Matching budget spent for found post {PostId}; the post stands without a match.", post.Id);
                break;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                _logger.LogWarning(error, "Matching agent skipped for found post {PostId} against report {ReportId}.", post.Id, lostReportId);
            }
        }
    }

    public async Task<PagedResult<FoundPostFeedItemDto>> GetFeedAsync(
        FoundPostQuery query,
        Guid? requesterId,
        CancellationToken cancellationToken = default)
    {
        var posts = _db.FoundReports
            .AsNoTracking()
            .Where(f => f.Status == FoundReportStatus.Posted);

        if (query.CategoryId is { } categoryId) posts = posts.Where(f => f.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            posts = posts.Where(f =>
                EF.Functions.ILike(f.GeneralDescription, term) ||
                (f.PrimaryColor != null && EF.Functions.ILike(f.PrimaryColor, term)) ||
                EF.Functions.ILike(f.ItemType.Name, term) ||
                EF.Functions.ILike(f.Category.Name, term) ||
                EF.Functions.ILike(f.FoundLocation.Name, term));
        }

        var ordered = posts.OrderByDescending(f => f.CreatedAt);
        var totalCount = await ordered.CountAsync(cancellationToken);
        var items = await ordered
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(Projection(requesterId))
            .ToListAsync(cancellationToken);

        return PagedResult<FoundPostFeedItemDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PagedResult<FoundPostFeedItemDto>> GetMineAsync(
        Guid finderId,
        PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        var posts = _db.FoundReports
            .AsNoTracking()
            .Where(f => f.FinderId == finderId)
            .OrderByDescending(f => f.CreatedAt);

        var totalCount = await posts.CountAsync(cancellationToken);
        var items = await posts.Skip(query.Skip).Take(query.PageSize).Select(Projection(finderId)).ToListAsync(cancellationToken);
        return PagedResult<FoundPostFeedItemDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<FoundPostMessageDto> SendMessageAsync(
        Guid postId,
        Guid senderId,
        string body,
        Guid? recipientId,
        CancellationToken cancellationToken = default)
    {
        var post = await _db.FoundReports
            .AsNoTracking()
            .Include(r => r.ItemType)
            .FirstOrDefaultAsync(r => r.Id == postId && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundAppException($"Found post '{postId}' was not found.");

        if (post.FinderId is not { } finderId)
            throw new ConflictAppException("This item was logged at a desk - ask the desk about it, not a finder.");

        if (post.Status is FoundReportStatus.Returned or FoundReportStatus.Disposed)
            throw new ConflictAppException("This item is no longer here to ask about.");

        var isFinder = finderId == senderId;
        Guid recipient;

        if (isFinder)
        {
            // The finder answers an existing thread and never opens one: first contact belongs
            // to the person who thinks it is theirs.
            recipient = recipientId
                ?? throw new ValidationAppException(nameof(SendFoundPostMessageRequest.RecipientId), "Say who the reply is to.");

            var threadExists = await _db.FoundReportMessages.AnyAsync(
                m => m.FoundReportId == postId && m.SenderId == recipient, cancellationToken);
            if (!threadExists)
                throw new ValidationAppException(nameof(SendFoundPostMessageRequest.RecipientId), "That person has not asked you about this item.");
        }
        else
        {
            if (recipientId is not null && recipientId != finderId)
                throw new ValidationAppException(nameof(SendFoundPostMessageRequest.RecipientId), "You can only write to the person who found it.");

            recipient = finderId;
        }

        var message = new FoundReportMessage
        {
            FoundReportId = postId,
            SenderId = senderId,
            RecipientId = recipient,
            Body = body.Trim(),
        };
        _db.FoundReportMessages.Add(message);

        _notifications.Queue(
            recipient,
            NotificationType.MessageReceived,
            isFinder
                ? $"A reply about the {post.ItemType.Name.ToLowerInvariant()} you asked about"
                : $"Someone is asking about the {post.ItemType.Name.ToLowerInvariant()} you found",
            // Quoted, not summarised - "you have a new message" makes someone open the app to
            // learn something they could have been told.
            message.Body.Length <= 140 ? message.Body : message.Body[..140] + "...",
            nameof(FoundReport),
            postId);

        await _db.SaveChangesAsync(cancellationToken);

        var names = await _db.Users.AsNoTracking()
            .Where(u => u.Id == senderId || u.Id == recipient)
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync(cancellationToken);

        return new FoundPostMessageDto(
            message.Id,
            names.First(n => n.Id == senderId).FullName,
            true,
            recipient,
            names.First(n => n.Id == recipient).FullName,
            message.Body,
            message.IsRead,
            message.CreatedAt);
    }

    public async Task<IReadOnlyList<FoundPostMessageDto>> GetMessagesAsync(
        Guid postId,
        Guid requesterId,
        bool requesterIsStaff,
        CancellationToken cancellationToken = default)
    {
        var post = await _db.FoundReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == postId && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundAppException($"Found post '{postId}' was not found.");

        var finderId = post.FinderId;
        var isFinder = finderId == requesterId;

        var messages = _db.FoundReportMessages.AsNoTracking().Where(m => m.FoundReportId == postId);

        // The finder reads every thread; staff read all to settle a dispute; an enquirer reads
        // only their own. Someone who has never written has nothing here.
        if (!isFinder && !requesterIsStaff)
        {
            var participates = await messages.AnyAsync(
                m => m.SenderId == requesterId || m.RecipientId == requesterId, cancellationToken);
            if (!participates)
                throw new ForbiddenAppException("You can only read messages you are part of.");

            messages = messages.Where(m => m.SenderId == requesterId || m.RecipientId == requesterId);
        }

        return await messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new FoundPostMessageDto(
                m.Id,
                m.Sender.FullName,
                m.SenderId == requesterId,
                // The counterpart is whoever is not the finder: the enquirer this thread is with.
                m.SenderId == finderId ? m.RecipientId : m.SenderId,
                m.SenderId == finderId ? m.Recipient.FullName : m.Sender.FullName,
                m.Body,
                m.IsRead,
                m.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<FoundPostFeedItemDto> RecogniseAsync(
        Guid id,
        Guid ownerId,
        RecogniseFoundPostRequest request,
        CancellationToken cancellationToken = default)
    {
        var post = await _db.FoundReports
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Found post '{id}' was not found.");

        if (post.Status != FoundReportStatus.Posted)
        {
            throw new ConflictAppException("This item has reached a desk - claim it from your reports instead.");
        }

        if (post.FinderId == ownerId)
        {
            throw new ConflictAppException("This is your own post.");
        }

        var report = await _db.LostReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.LostReportId, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{request.LostReportId}' was not found.");

        if (report.StudentId != ownerId)
        {
            throw new ForbiddenAppException("You can only match an item to your own report.");
        }

        // The suggestion is the same row a desk or the agent would write; the owner's word
        // carries no more weight than theirs, and the claim's questions still decide.
        await _suggestions.CreateAsync(
            new CreateMatchSuggestionRequest(report.Id, post.Id, "You recognised this on the feed."),
            ownerId,
            cancellationToken);

        if (post.FinderId is { } finderId)
        {
            _notifications.Queue(
                finderId,
                NotificationType.FoundPostRecognised,
                "Someone thinks the item you found is theirs",
                "Please hand it in at any campus desk and quote your hand-in code. The desk will make sure it goes to the right person.",
                nameof(FoundReport),
                post.Id);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return await LoadAsync(post.Id, ownerId, cancellationToken);
    }

    public async Task<FoundPostFeedItemDto> WithdrawAsync(
        Guid id,
        Guid finderId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var post = await _db.FoundReports.FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Found post '{id}' was not found.");

        if (post.FinderId != finderId)
        {
            throw new ForbiddenAppException("You can only take down your own posts.");
        }

        if (post.Status != FoundReportStatus.Posted)
        {
            throw new ConflictAppException("This item has reached a desk - it is theirs to manage now.");
        }

        _db.FoundReportStatusHistories.Add(new FoundReportStatusHistory
        {
            FoundReportId = post.Id,
            FromStatus = post.Status,
            ToStatus = FoundReportStatus.Disposed,
            ChangedByUserId = finderId,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Taken down by the finder." : reason.Trim(),
        });
        post.Status = FoundReportStatus.Disposed;
        post.HandInCode = null;
        post.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadAsync(post.Id, finderId, cancellationToken);
    }

    public async Task<FoundPostFeedItemDto> DeclareHandedInAsync(
        Guid id,
        Guid finderId,
        CancellationToken cancellationToken = default)
    {
        var post = await _db.FoundReports
            .FirstOrDefaultAsync(f => f.Id == id && f.FinderId == finderId, cancellationToken)
            ?? throw new NotFoundAppException($"Found post '{id}' was not found.");

        if (post.Status != FoundReportStatus.Posted)
            throw new ConflictAppException("This post has already been received by a desk.");

        if (post.HandedToSecurityAt is null)
        {
            post.HandedToSecurityAt = DateTime.UtcNow;
            post.UpdatedAt = DateTime.UtcNow;
            _db.FoundReportStatusHistories.Add(new FoundReportStatusHistory
            {
                FoundReportId = post.Id,
                FromStatus = FoundReportStatus.Posted,
                ToStatus = FoundReportStatus.Posted,
                ChangedByUserId = finderId,
                Reason = "Finder declared that the item was handed to security.",
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return await LoadAsync(post.Id, finderId, cancellationToken);
    }

    public async Task<FoundReportDetailDto> GetByHandInCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalised = code.Replace(" ", "");
        if (!HandoverCodes.LooksValid(normalised))
        {
            throw new ValidationAppException("code", "A hand-in code is six digits.");
        }

        var id = await _db.FoundReports
            .Where(f => f.HandInCode == normalised && f.Status == FoundReportStatus.Posted)
            .Select(f => (Guid?)f.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundAppException("No open post has that code.");

        return await _foundReports.GetByIdAsync(id, cancellationToken);
    }

    public async Task<FoundReportDetailDto> ConfirmAsync(
        Guid id,
        Guid staffId,
        ConfirmFoundPostRequest request,
        CancellationToken cancellationToken = default)
    {
        // The item type and place are read further down for the honor award's one-line
        // description, so they are loaded here rather than lazily - there is no lazy loading.
        var post = await _db.FoundReports
            .Include(f => f.ItemType)
            .Include(f => f.FoundLocation)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Found post '{id}' was not found.");

        if (post.Status != FoundReportStatus.Posted)
        {
            throw new ConflictAppException("This item is already at a desk.");
        }

        if (!await _db.StorageLocations.AnyAsync(s => s.Id == request.StorageLocationId, cancellationToken))
        {
            throw new NotFoundAppException($"Storage location '{request.StorageLocationId}' was not found.");
        }

        post.StaffId = staffId;
        post.StorageLocationId = request.StorageLocationId;
        post.PrivateVerificationDetails = string.IsNullOrWhiteSpace(request.PrivateVerificationDetails)
            ? null
            : request.PrivateVerificationDetails.Trim();
        if (!string.IsNullOrWhiteSpace(request.GeneralDescription))
        {
            post.GeneralDescription = request.GeneralDescription.Trim();
        }
        // The finder's code has done its job; the record is the desk's now.
        post.HandInCode = null;

        _db.FoundReportStatusHistories.Add(new FoundReportStatusHistory
        {
            FoundReportId = post.Id,
            FromStatus = post.Status,
            ToStatus = FoundReportStatus.Unclaimed,
            ChangedByUserId = staffId,
            Reason = "Handed in and confirmed at the desk.",
        });
        post.Status = FoundReportStatus.Unclaimed;
        post.UpdatedAt = DateTime.UtcNow;

        if (post.FinderId is { } finderId)
        {
            _notifications.Queue(
                finderId,
                NotificationType.FoundPostConfirmed,
                "Thank you - the desk has it",
                "The item you posted is safely in storage. If its owner comes forward, they will collect it from there.",
                nameof(FoundReport),
                post.Id);

            // A desk has the item in its hands: the one outcome nobody can award themselves.
            await _honor.QueueAwardAsync(
                finderId,
                HonorAwardReason.HandedInAtDesk,
                null,
                post.Id,
                $"Handed in a {post.ItemType.Name.ToLowerInvariant()} at {post.FoundLocation.Name}",
                cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await _foundReports.GetByIdAsync(post.Id, cancellationToken);
    }

    /* ------------------------------------------------------------------ internals */

    private async Task<string> NextHandInCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = HandoverCodes.Generate();
            if (!await _db.FoundReports.AnyAsync(f => f.HandInCode == code, cancellationToken)) return code;
        }

        throw new InvalidOperationException("Could not allocate a unique hand-in code.");
    }

    private async Task<FoundPostFeedItemDto> LoadAsync(Guid id, Guid? requesterId, CancellationToken cancellationToken)
        => await _db.FoundReports
            .AsNoTracking()
            .Where(f => f.Id == id)
            .Select(Projection(requesterId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundAppException($"Found post '{id}' was not found.");

    /// <summary>
    /// The finder's display name and nothing else identifying, like the lost feed. The code
    /// only travels to the finder themselves - it is what they quote at the desk.
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<FoundReport, FoundPostFeedItemDto>> Projection(Guid? requesterId)
        => f => new FoundPostFeedItemDto(
            f.Id,
            f.Finder == null ? "A student" : f.Finder.FullName,
            requesterId != null && f.FinderId == requesterId,
            f.Category.Name,
            f.ItemType.Name,
            f.FoundLocation.Name,
            f.GeneralDescription,
            f.PrimaryColor,
            f.FoundAt,
            f.Status.ToString(),
            requesterId != null && f.FinderId == requesterId ? f.HandInCode : null,
            requesterId != null && f.FinderId == requesterId ? f.HandedToSecurityAt : null,
            f.CreatedAt);
}
