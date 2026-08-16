namespace FoundU.Application.Common.Pagination;

/// <summary>
/// Standard pagination input for every list endpoint in FoundU. See
/// /docs/api/conventions.md "Pagination" for the query-string contract every feature must follow.
/// </summary>
public class PaginationQuery
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>1-based page number. Values below 1 are clamped to 1.</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Items per page. Clamped between 1 and 100 to prevent unbounded queries.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    /// <summary>Optional free-text search term - meaning is feature-specific (see each endpoint's docs).</summary>
    public string? Search { get; set; }

    /// <summary>Column name to sort by - feature-specific allow-list, never raw/unvalidated SQL.</summary>
    public string? SortBy { get; set; }

    /// <summary>"asc" or "desc". Defaults to "asc" if omitted or invalid.</summary>
    public string? SortDirection { get; set; }

    public int Skip => (Page - 1) * PageSize;
}
