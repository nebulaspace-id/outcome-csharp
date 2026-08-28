namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Cursor-pagination metadata sent in the response body. Implements
/// <see cref="IPaginationDetails"/> so the pipeline works with either offset or cursor
/// pagination through one type.
///
/// <para>
/// Use <see cref="Create{T}"/> from the endpoint to build the metadata in one call from
/// the incoming <see cref="DefaultCursorQuery"/> and the service's <see cref="DefaultCursorResult{T}"/>.
/// </para>
/// </summary>
public sealed class DefaultCursorInfo : IPaginationDetails
{
    /// <summary>
    /// Opaque cursor pointing to the previous page. <c>null</c> when this is the first
    /// page (omitted from JSON via <c>JsonIgnoreCondition.WhenWritingNull</c>).
    /// </summary>
    public string? PreviousCursor { get; set; }

    /// <summary>
    /// Opaque cursor pointing to the next page. <c>null</c> when this is the last page
    /// (omitted from JSON via <c>JsonIgnoreCondition.WhenWritingNull</c>).
    /// </summary>
    public string? NextCursor { get; set; }

    /// <summary>
    /// Whether there is at least one more page of data after this one — from
    /// <see cref="DefaultCursorResult{T}.HasMore"/>.
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// Total items across all pages, when the service provides it.
    /// <c>null</c> when unknown (omitted from JSON via
    /// <c>JsonIgnoreCondition.WhenWritingNull</c>).
    /// </summary>
    public long? TotalItems { get; set; }

    /// <summary>
    /// Creates <see cref="DefaultCursorInfo"/> from the incoming query and the service's
    /// cursor-paginated result — the idiomatic one‑line call from the endpoint.
    /// </summary>
    /// <typeparam name="T">The type of each item in the page.</typeparam>
    /// <param name="query">The incoming cursor-pagination request.</param>
    /// <param name="result">The service's cursor-paginated result.</param>
    public static DefaultCursorInfo Create<T>(DefaultCursorQuery query, DefaultCursorResult<T> result) => new()
    {
        PreviousCursor = result.PreviousCursor,
        NextCursor = result.NextCursor,
        HasMore = result.HasMore,
        TotalItems = result.TotalCount,
    };
}
