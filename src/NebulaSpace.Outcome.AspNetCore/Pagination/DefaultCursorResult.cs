namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// A single page of items from a cursor-paginated query, together with opaque cursors for
/// the previous and next pages. The service returns this and the endpoint, which already
/// has the incoming <see cref="DefaultCursorQuery"/>, constructs the <see cref="DefaultCursorPagination"/>
/// metadata from it — separating the query intent (cursor/limit) from the raw data the
/// service reports (items + cursors + count).
///
/// <para>
/// Use <see cref="DefaultCursorPagination.Create{T}(DefaultCursorQuery, DefaultCursorResult{T})"/> to build the
/// metadata from this result in one call.
/// </para>
/// </summary>
/// <typeparam name="T">The type of each item in the page.</typeparam>
public sealed class DefaultCursorResult<T>
{
    /// <summary>Items in the current page (never <c>null</c>).</summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>
    /// Opaque cursor pointing to the previous page. <c>null</c> when this is the first
    /// page.
    /// </summary>
    public string? PreviousCursor { get; set; }

    /// <summary>
    /// Opaque cursor pointing to the next page. <c>null</c> when this is the last page.
    /// </summary>
    public string? NextCursor { get; set; }

    /// <summary>
    /// Whether there is at least one more page of data after this one.
    /// Equivalent to <c>NextCursor is not null</c>.
    /// </summary>
    public bool HasMore => NextCursor is not null;

    /// <summary>Total count of items across all pages, when known.</summary>
    public long? TotalCount { get; set; }

    /// <summary>
    /// Builds <see cref="DefaultCursorPagination"/> from this result and the incoming query —
    /// convenience over <c>DefaultCursorInfo.Create(query, result)</c>.
    /// </summary>
    /// <param name="query">The incoming cursor-pagination request.</param>
    public DefaultCursorPagination ToCursorPagination(DefaultCursorQuery query) =>
        DefaultCursorPagination.Create(query, this);
}
