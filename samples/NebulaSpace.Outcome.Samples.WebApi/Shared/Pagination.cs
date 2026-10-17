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
}