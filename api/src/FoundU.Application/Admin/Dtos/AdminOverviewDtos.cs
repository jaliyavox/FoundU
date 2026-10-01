namespace FoundU.Application.Admin.Dtos;

/// <summary>
/// What needs a person today, in one call - the admin panel's front page.
///
/// Every number here is something somebody can act on: a queue, a backlog, a thing waiting.
/// Totals that only describe the past live on the analytics page instead.
/// </summary>
public record AdminOverviewDto(
    AdminQueueDto Queues,
    AdminPeopleDto People,
    IReadOnlyList<AdminActivityDto> Recent);

public record AdminQueueDto(
    int ClaimsAwaitingDecision,
    int ClaimsApprovedNotCollected,
    int ItemsPostedAwaitingHandIn,
    int ItemsUnclaimedInStorage,
    int FlaggedReports,
    int SupportOpen,
    int SupportUnassigned,
    int OldestSupportHours);

public record AdminPeopleDto(int Students, int Staff, int Admins, int Suspended, int NewThisWeek);

/// <summary>One line of "what just happened", newest first.</summary>
public record AdminActivityDto(string Kind, string Summary, DateTime At);
