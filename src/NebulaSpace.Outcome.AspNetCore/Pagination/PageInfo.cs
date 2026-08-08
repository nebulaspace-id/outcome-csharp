namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Pagination metadata sent in the response body — current page, page size, total pages,
/// and total items. Built once from the incoming <see cref="PageQuery"/> and the total item
/// count, so the client knows exactly where it is in the result set.
///
/// <para>
/// Use <see cref="Create(PageQuery,int)"/> from the service layer to construct it in one call.
/// </para>
/// </summary>
public sealed class PageInfo
{
    /// <summary>The current 1‑based page number.</summary>
    public int Page { get; set; }

    /// <summary>Items per page the server returned for this request.</summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages (<c>0</c> when there are no items, otherwise
    /// <c>⌈totalItems / pageSize⌉</c>).
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>Total items across all pages.</summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Creates <see cref="PageInfo"/> from raw pagination values, computing
    /// <see cref="TotalPages"/> from <paramref name="totalItems"/> and
    /// <paramref name="pageSize"/>.
    /// </summary>
    public static PageInfo Create(int page, int pageSize, int totalItems) => new()
    {
        Page = page,
        PageSize = pageSize,
        TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / pageSize),
        TotalItems = totalItems,
    };

    /// <summary>
    /// Creates <see cref="PageInfo"/> from a <see cref="PageQuery"/> and the total item
    /// count — the idiomatic one‑line call from the service layer.
    /// </summary>
    /// <param name="query">The incoming pagination request.</param>
    /// <param name="totalItems">Total number of items across all pages.</param>
    public static PageInfo Create(PageQuery query, int totalItems) =>
        Create(query.Page, query.PageSize, totalItems);
}
