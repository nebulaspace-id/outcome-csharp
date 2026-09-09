using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Custom cursor-pagination metadata sent in the response body. Implements
/// <see cref="IPaginationDetails"/> so the library resolves the concrete type at
/// runtime and System.Text.Json / Swagger serialize the full shape — <c>previousCursor</c>,
/// <c>nextCursor</c>, <c>hasMore</c>, and <c>totalItems</c>.
///
/// <para>
/// Build via <see cref="Create{T}"/> or <see cref="CursorResult{T}.ToCursorPagination"/>
/// — one line from the endpoint.
/// </para>
/// </summary>
public sealed class CursorPagination : IPaginationDetails
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
    /// <see cref="CursorResult{T}.HasMore"/>.
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// Total items across all pages, when the service provides it.
    /// <c>null</c> when unknown (omitted from JSON via
    /// <c>JsonIgnoreCondition.WhenWritingNull</c>).
    /// </summary>
    public long? TotalItems { get; set; }

    /// <summary>
    /// Creates <see cref="CursorPagination"/> from the incoming query and the service's
    /// cursor-paginated result — the idiomatic one‑line call from the endpoint.
    /// </summary>
    /// <typeparam name="T">The type of each item in the page.</typeparam>
    /// <param name="result">The service's cursor-paginated result.</param>
    public static CursorPagination Create<T>(CursorResult<T> result) => new()
    {
        PreviousCursor = result.PreviousCursor,
        NextCursor = result.NextCursor,
        HasMore = result.HasMore,
        TotalItems = result.TotalCount,
    };
}
