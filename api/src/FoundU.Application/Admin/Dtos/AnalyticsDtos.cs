namespace FoundU.Application.Admin.Dtos;

/// <summary>
/// Real counts, grouped. Nothing here is derived from anything but rows in the database -
/// a dashboard number that is estimated is indistinguishable from one that is measured.
/// </summary>
public record StatusCountDto(string Status, int Count);

/// <summary>One calendar day of desk activity, in UTC.</summary>
public record DailyActivityDto(DateOnly Date, int LostReported, int ItemsLoggedIn, int ItemsReturned);

public record CategoryActivityDto(string Category, int Lost, int Found);

public record AnalyticsOverviewDto(
    IReadOnlyList<StatusCountDto> LostReportsByStatus,
    IReadOnlyList<StatusCountDto> FoundItemsByStatus,
    IReadOnlyList<StatusCountDto> ClaimsByStatus,
    /// <summary>The last 30 days, oldest first, with zero-filled days so the chart has no gaps.</summary>
    IReadOnlyList<DailyActivityDto> Last30Days,
    /// <summary>Top categories by lost + found, at most five.</summary>
    IReadOnlyList<CategoryActivityDto> TopCategories,
    /// <summary>Mean days from a lost report being posted to its claim being approved. Null until one has.</summary>
    double? AverageDaysToReturn,
    int ResolvedReports,
    int OpenFlags,
    DateTime GeneratedAt);
