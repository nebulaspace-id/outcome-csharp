namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Incoming cursor-pagination parameters. Bound from the query string with
/// <c>[AsParameters]</c> — the parameterless constructor applies the default limit.
///
/// <para>
/// <see cref="Limit"/> is clamped to <c>1..<see cref="MaxLimit"/></c> so a hostile query
/// cannot request an unbounded page. The cap is a constant — custom query types can apply
/// their own policy.
/// </para>
/// </summary>
public sealed class DefaultCursorQuery : IPaginatedQuery
{
    /// <summary>Maximum items per page a client can request. Defaults to <c>100</c>.</summary>
    public const int MaxLimit = 100;

    private string? _cursor;
    private int _limit = 10;

    /// <summary>
    /// Parameterless constructor for <c>[AsParameters]</c> binding — applies the
    /// default limit.
    /// </summary>
    public DefaultCursorQuery() { }

    /// <summary>
    /// Opaque cursor pointing to the start of the next page. <c>null</c> for the first
    /// page. Decoding and encoding is the service's responsibility — the ASP.NET Core
    /// layer treats cursors as opaque strings.
    /// </summary>
    public string? Cursor
    {
        get => _cursor;
        set => _cursor = value;
    }

    /// <summary>Items per page, clamped to <c>1..MaxLimit</c>.</summary>
    public int Limit
    {
        get => _limit;
        set => _limit = Math.Clamp(value, 1, MaxLimit);
    }
}
