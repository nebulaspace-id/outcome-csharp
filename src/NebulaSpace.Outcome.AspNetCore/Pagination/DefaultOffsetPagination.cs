namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Offset-pagination metadata sent in the response body — current page, items per page
/// (<see cref="Limit"/>), total pages, and total items. Implements
/// <see cref="IPaginationDetails"/> so the pipeline works with either offset or cursor
/// pagination through one type.
///
/// <para>
/// Use <see cref="Create{T}"/> from the endpoint to build the metadata in one call from
/// the service's <see cref="DefaultOffsetResult{T}"/> and the incoming
/// <see cref="DefaultOffsetQuery"/>.
/// </para>
/// </summary>
public sealed class DefaultOffsetPagination : IPaginationDetails
{
    /// <summary>
    /// Whether there is at least one more page after this one,
    /// i.e. <c>Page &lt; TotalPages</c>.
    /// </summary>
    public bool HasMore => Page < TotalPages;

    /// <summary>The current 1‑based page number.</summary>
    public int Page { get; init; }

    /// <summary>Items per page the server returned for this request.</summary>
    public int Limit { get; init; }

    /// <summary>
    /// Total number of pages (<c>0</c> when there are no items, otherwise
    /// <c>⌈totalItems / limit⌉</c>).
    /// </summary>
    public int TotalPages { get; init; }

    /// <summary>Total items across all pages, or <c>null</c> when unknown.</summary>
    public long? TotalItems { get; init; }

    /// <summary>
    /// Creates <see cref="DefaultOffsetPagination"/> from the service's result and the
    /// incoming query — the idiomatic one‑line call from the endpoint. The request's
    /// <see cref="Page"/>/<see cref="Limit"/> are echoed as the client's intent, while
    /// <see cref="TotalPages"/> is derived from the service's total count.
    /// </summary>
    public static DefaultOffsetPagination Create<T>(DefaultOffsetResult<T> result, DefaultOffsetQuery query) =>
        Create(query.Page, query.Limit, (int)result.TotalCount);

    /// <summary>
    /// Creates <see cref="DefaultOffsetPagination"/> from raw pagination values, computing
    /// <see cref="TotalPages"/> from <paramref name="totalItems"/> and
    /// <paramref name="limit"/>.
    /// </summary>
    public static DefaultOffsetPagination Create(int page, int limit, int totalItems) => new()
    {
        Page = page,
        Limit = limit,
        TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / limit),
        TotalItems = totalItems,
    };
}
