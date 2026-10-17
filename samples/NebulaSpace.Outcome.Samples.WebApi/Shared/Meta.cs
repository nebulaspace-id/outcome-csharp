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
    /// Builds the <b>offset</b> pagination fields — <c>page</c>, <c>limit</c>,
    /// <c>totalItems</c>, <c>totalPages</c>, and <c>hasMore</c> — from the service's
    /// <see cref="OffsetResult{T}"/> and the incoming query.
    /// </summary>
    /// <remarks>
    /// <see cref="Page"/> and <see cref="Limit"/> echo the request because they are the
    /// client's intent; <see cref="TotalPages"/> and <see cref="HasMore"/> are derived from
    /// the service's <see cref="OffsetResult{T}.TotalCount"/>.
    /// </remarks>
    public Meta WithOffsetPagination<T>(OffsetResult<T> result, OffsetQuery query)
    {
        var totalPages = result.TotalCount == 0
            ? 0
            : (int)Math.Ceiling((double)result.TotalCount / query.Limit);

        return new Meta
        {
            Page = query.Page,
            Limit = query.Limit,
            TotalItems = result.TotalCount,
            TotalPages = totalPages,
            HasMore = query.Page < totalPages,
        };
    }

    /// <summary>
    /// Builds the <b>cursor</b> pagination fields — <c>previousCursor</c>,
    /// <c>nextCursor</c>, <c>hasMore</c>, and <c>totalItems</c> — from the service's
    /// <see cref="CursorResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// Takes no query: a cursor page is self-describing, so nothing is echoed from the
    /// request — the service hands back both cursors and the count.
    /// </remarks>
    public Meta WithCursorPagination<T>(CursorResult<T> result) => new()
    {
        PreviousCursor = result.PreviousCursor,
        NextCursor = result.NextCursor,
        HasMore = result.HasMore,
        TotalItems = result.TotalCount,
    };
}
