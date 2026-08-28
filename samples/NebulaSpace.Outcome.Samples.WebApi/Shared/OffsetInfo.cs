using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Custom offset-pagination metadata sent in the response body. Implements
/// <see cref="IPaginationDetails"/> so the library resolves the concrete type at
/// runtime and System.Text.Json / Swagger serialize the full shape — <c>page</c>,
/// <c>limit</c>, <c>totalPages</c>, <c>totalItems</c>, and <c>hasMore</c>.
///
/// <para>
/// Build via <see cref="Create(OffsetQuery,int)"/> or <see cref="OffsetResult{T}.ToOffsetInfo"/>
/// — one line from the endpoint.
/// </para>
/// </summary>
public sealed class OffsetInfo : IPaginationDetails
{
    /// <summary>
    /// Whether there is at least one more page after this one,
    /// i.e. <c>Page &lt; TotalPages</c>.
    /// </summary>
    public bool HasMore => Page < TotalPages;

    /// <summary>The current 1‑based page number.</summary>
    public int Page { get; set; }

    /// <summary>Items per page the server returned for this request.</summary>
    public int Limit { get; set; }

    /// <summary>
    /// Total number of pages (<c>0</c> when there are no items, otherwise
    /// <c>⌈totalItems / limit⌉</c>).
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>Total items across all pages, or <c>null</c> when unknown.</summary>
    public long? TotalItems { get; set; }

    /// <summary>
    /// Creates <see cref="OffsetInfo"/> from raw pagination values, computing
    /// <see cref="TotalPages"/> from <paramref name="totalItems"/> and
    /// <paramref name="limit"/>.
    /// </summary>
    public static OffsetInfo Create(int page, int limit, int totalItems) => new()
    {
        Page = page,
        Limit = limit,
        TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / limit),
        TotalItems = totalItems,
    };

    /// <summary>
    /// Creates <see cref="OffsetInfo"/> from an <see cref="OffsetQuery"/> and the total item
    /// count — the idiomatic one‑line call from the service layer.
    /// </summary>
    /// <param name="query">The incoming pagination request.</param>
    /// <param name="totalItems">Total number of items across all pages.</param>
    public static OffsetInfo Create(OffsetQuery query, int totalItems) =>
        Create(query.Page, query.Limit, totalItems);
}
