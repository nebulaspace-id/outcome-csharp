namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Offset-pagination metadata sent in the response body — current page, items per page
/// (<see cref="Limit"/>), total pages, and total items. Implements
/// <see cref="IPaginationDetails"/> so the pipeline works with either offset or cursor
/// pagination through one type.
///
/// <para>
/// Use <see cref="Create{T}(DefaultOffsetResult{T}, DefaultOffsetQuery)"/> from the
/// endpoint to build the metadata in one call from the service's
/// <see cref="DefaultOffsetResult{T}"/> and the incoming <see cref="DefaultOffsetQuery"/>.
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

    /// <summary>Total items across all pages, when known.</summary>
    public long? TotalItems { get; init; }

    /// <summary>
    /// Creates <see cref="DefaultOffsetPagination"/> from the service's result and the
    /// incoming query — the idiomatic one‑line call from the endpoint. The request's
    /// <see cref="Page"/>/<see cref="Limit"/> are echoed as the client's intent, while
    /// <see cref="TotalPages"/> is derived from the service's total count.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="result"/> or <paramref name="query"/> is <c>null</c>.
    /// </exception>
    public static DefaultOffsetPagination Create<T>(DefaultOffsetResult<T> result, DefaultOffsetQuery query)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));
        if (query is null) throw new ArgumentNullException(nameof(query));

        return Create(query.Page, query.Limit, result.TotalCount);
    }

    /// <summary>
    /// Creates <see cref="DefaultOffsetPagination"/> from raw pagination values, computing
    /// <see cref="TotalPages"/> from <paramref name="totalItems"/> and
    /// <paramref name="limit"/>. All arithmetic is 64‑bit so large counts cannot overflow
    /// into a negative page count.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> or <paramref name="limit"/> is less than 1, or
    /// <paramref name="totalItems"/> is negative.
    /// </exception>
    public static DefaultOffsetPagination Create(int page, int limit, long totalItems)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), page, "Page must be at least 1.");
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be at least 1.");
        if (totalItems < 0) throw new ArgumentOutOfRangeException(nameof(totalItems), totalItems, "Total items cannot be negative.");

        return new DefaultOffsetPagination
        {
            Page = page,
            Limit = limit,
            TotalPages = ToTotalPages(totalItems, limit),
            TotalItems = totalItems,
        };
    }

    /// <summary>Creates <see cref="DefaultOffsetPagination"/> from raw <see cref="int"/> values.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> or <paramref name="limit"/> is less than 1, or
    /// <paramref name="totalItems"/> is negative.
    /// </exception>
    public static DefaultOffsetPagination Create(int page, int limit, int totalItems) =>
        Create(page, limit, (long)totalItems);

    private static int ToTotalPages(long totalItems, int limit)
    {
        if (totalItems == 0) return 0;

        // (n - 1) / limit + 1 avoids the overflow of (n + limit - 1) when n is near long.MaxValue.
        var pages = (totalItems - 1) / limit + 1;
        return pages >= int.MaxValue ? int.MaxValue : (int)pages;
    }
}
