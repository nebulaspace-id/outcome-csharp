using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// The page details for a paged response, nested under <see cref="Meta.Pagination"/>.
/// One type covers both strategies: every field is nullable, so
/// <c>JsonIgnoreCondition.WhenWritingNull</c> omits the ones the strategy in play does
/// not fill — an offset response emits <c>page</c>/<c>limit</c>/<c>totalItems</c>/
/// <c>totalPages</c>, a cursor response emits <c>previousCursor</c>/<c>nextCursor</c>,
/// and both share <c>hasMore</c>.
/// </summary>
public sealed class Pagination
{
    // ── Offset pagination — null unless the response is offset-paged ──────────

    /// <summary>The current 1-based page number.</summary>
    public int? Page { get; init; }

    /// <summary>Items per page the server returned for this request.</summary>
    public int? Limit { get; init; }

    /// <summary>Total items across all pages, when known.</summary>
    public long? TotalItems { get; init; }

    /// <summary>Total number of pages (<c>0</c> when there are no items).</summary>
    public int? TotalPages { get; init; }

    // ── Shared by offset and cursor ───────────────────────────────────────────

    /// <summary>Whether at least one more page follows this one.</summary>
    public bool? HasMore { get; init; }

    // ── Cursor pagination — null unless the response is cursor-paged ──────────

    /// <summary>Opaque cursor pointing to the previous page; <c>null</c> on the first page.</summary>
    public string? PreviousCursor { get; init; }

    /// <summary>Opaque cursor pointing to the next page; <c>null</c> on the last page.</summary>
    public string? NextCursor { get; init; }

    // ── Factories — one per pagination strategy ───────────────────────────────

    /// <summary>
    /// Builds the <b>offset</b> pagination details from the service's
    /// <see cref="OffsetResult{T}"/> and the incoming query: the request's
    /// <see cref="Page"/> and <see cref="Limit"/> are echoed as the client's intent,
    /// while <see cref="TotalPages"/> and <see cref="HasMore"/> are derived from the
    /// service's <see cref="OffsetResult{T}.TotalCount"/>.
    /// </summary>
    public static Pagination Offset<T>(OffsetResult<T> result, OffsetQuery query)
    {
        // Reuse the library's 64-bit page math instead of re-deriving it: the sample
        // cannot drift from the shipped DefaultOffsetPagination behavior.
        var page = DefaultOffsetPagination.Create(query.Page, query.Limit, result.TotalCount);

        return new Pagination
        {
            Page = page.Page,
            Limit = page.Limit,
            TotalItems = page.TotalItems,
            TotalPages = page.TotalPages,
            HasMore = page.HasMore,
        };
    }

    /// <summary>
    /// Builds the <b>cursor</b> pagination details from the service's
    /// <see cref="CursorResult{T}"/>. Takes no query: a cursor page is self-describing,
    /// so nothing is echoed from the request — the service hands back both cursors and
    /// the count.
    /// </summary>
    public static Pagination Cursor<T>(CursorResult<T> result) => new()
    {
        PreviousCursor = result.PreviousCursor,
        NextCursor = result.NextCursor,
        HasMore = result.HasMore,
        TotalItems = result.TotalCount,
    };
}