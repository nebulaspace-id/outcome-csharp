namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// The response metadata this API attaches to every success envelope — when the response
/// was produced (<see cref="Timestamp"/>) and, when the response is paged, the page
/// details nested under <see cref="Pagination"/>.
///
/// <para>
/// Built in the <b>endpoint layer</b>; the library only carries it through to the
/// envelope and never inspects it.
/// </para>
/// </summary>
/// <remarks>
/// <see cref="Pagination"/> is <c>null</c> for an unpaged response, so
/// <c>JsonIgnoreCondition.WhenWritingNull</c> omits the whole key — an unpaged response
/// emits just <c>timestamp</c>.
/// </remarks>
public sealed class Meta
{
    /// <summary>
    /// The page details for a paged response, or <c>null</c> when the response is not
    /// paged — the key is then omitted from the body entirely.
    /// </summary>
    public Pagination? Pagination { get; private init; }

    /// <summary>When this response was produced (UTC).</summary>
    public DateTimeOffset Timestamp { get; private init; } = DateTimeOffset.UtcNow;

    // ── With — one per pagination strategy ────────────────────────────────────

    /// <summary>
    /// Nests the <b>offset</b> pagination details — <c>page</c>, <c>limit</c>,
    /// <c>totalItems</c>, <c>totalPages</c>, and <c>hasMore</c> — built from the
    /// service's <see cref="OffsetResult{T}"/> and the incoming query.
    /// </summary>
    /// <remarks>
    /// <see cref="Pagination.Page"/> and <see cref="Pagination.Limit"/> echo the request
    /// because they are the client's intent; <see cref="Pagination.TotalPages"/> and
    /// <see cref="Pagination.HasMore"/> are derived from the service's
    /// <see cref="OffsetResult{T}.TotalCount"/>.
    /// </remarks>
    public Meta WithOffsetPagination<T>(OffsetResult<T> result, OffsetQuery query)
    {
        var totalPages = result.TotalCount == 0
            ? 0
            : (int)Math.Ceiling((double)result.TotalCount / query.Limit);

        return new Meta
        {
            Pagination = new Pagination
            {
                Page = query.Page,
                Limit = query.Limit,
                TotalItems = result.TotalCount,
                TotalPages = totalPages,
                HasMore = query.Page < totalPages,
            },
        };
    }

    /// <summary>
    /// Nests the <b>cursor</b> pagination details — <c>previousCursor</c>,
    /// <c>nextCursor</c>, <c>hasMore</c>, and <c>totalItems</c> — from the service's
    /// <see cref="CursorResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// Takes no query: a cursor page is self-describing, so nothing is echoed from the
    /// request — the service hands back both cursors and the count.
    /// </remarks>
    public Meta WithCursorPagination<T>(CursorResult<T> result) => new()
    {
        Pagination = new Pagination
        {
            PreviousCursor = result.PreviousCursor,
            NextCursor = result.NextCursor,
            HasMore = result.HasMore,
            TotalItems = result.TotalCount,
        },
    };
}