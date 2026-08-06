namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Incoming pagination parameters. When bound from the query string (e.g. with
/// <c>[AsParameters]</c>), the parameterless constructor applies the default limits.
/// Values outside the bounds are clamped, never reset — <c>?page=999</c> becomes the
/// last page allowed (25 by default), not page 1.
/// </summary>
public sealed class PageQuery
{
    public const int DefaultMaxPage = 25;
    public const int DefaultMaxPageSize = 50;

    private int _page = 1;
    private int _pageSize = 10;
    private readonly int _maxPage = DefaultMaxPage;
    private readonly int _maxPageSize = DefaultMaxPageSize;

    public PageQuery() { }

    /// <summary>
    /// Creates a query with custom limits (clamped to at least 1). Use for manual
    /// construction; query-string binding uses the parameterless constructor.
    /// </summary>
    public PageQuery(int maxPage, int maxPageSize)
    {
        _maxPage = Math.Max(1, maxPage);
        _maxPageSize = Math.Max(1, maxPageSize);
    }

    /// <summary>1-based page number, clamped to <c>1..MaxPage</c>.</summary>
    public int Page
    {
        get => _page;
        set => _page = Math.Clamp(value, 1, MaxPage);
    }

    /// <summary>Items per page, clamped to <c>1..MaxPageSize</c>.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }

    /// <summary>Maximum page number allowed by this query (25 by default).</summary>
    public int MaxPage => _maxPage;

    /// <summary>Maximum page size allowed by this query (50 by default).</summary>
    public int MaxPageSize => _maxPageSize;

    /// <summary>
    /// The 0-based offset of the first item in this page, i.e. <c>(Page - 1) * PageSize</c>.
    /// Convenience for direct use with <c>Skip()</c>.
    /// </summary>
    public int Offset => (Page - 1) * PageSize;
}
