using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// The response metadata this API attaches to every success envelope — timestamp plus,
/// when the response is paged, the page fields flattened directly onto it (JSON:API style:
/// no nested <c>pagination</c> object).
///
/// <para>
/// Built in the <b>endpoint layer</b>; the library only carries it through to the
/// envelope and never inspects it.
/// </para>
/// </summary>
/// <remarks>
/// Every pagination field is nullable, so <c>JsonIgnoreCondition.WhenWritingNull</c> omits
/// the ones that do not apply — an unpaged response emits just <c>timestamp</c>, while
/// offset and cursor responses each emit their own subset.
/// </remarks>
public sealed class Meta
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

    // ── Per-request ───────────────────────────────────────────────────────────

    /// <summary>When this response was produced (UTC).</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    // ── With — one per pagination strategy ────────────────────────────────────

    /// <summary>
    /// Flattens <b>offset</b> pagination — <c>page</c>, <c>limit</c>, <c>totalItems</c>,
    /// <c>totalPages</c>, and <c>hasMore</c> — onto this metadata.
    /// </summary>
    public Meta WithOffsetPagination(OffsetPagintion pagination) => new()
    {
        Page = pagination.Page,
        Limit = pagination.Limit,
        TotalItems = pagination.TotalItems,
        TotalPages = pagination.TotalPages,
        HasMore = pagination.HasMore,
    };

    /// <summary>
    /// Flattens <b>cursor</b> pagination — <c>previousCursor</c>, <c>nextCursor</c>,
    /// <c>hasMore</c>, and <c>totalItems</c> — onto this metadata.
    /// </summary>
    public Meta WithCursorPagination(CursorPagination pagination) => new()
    {
        PreviousCursor = pagination.PreviousCursor,
        NextCursor = pagination.NextCursor,
        HasMore = pagination.HasMore,
        TotalItems = pagination.TotalItems,
    };
}
