namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Incoming cursor-pagination parameters. Bound from the query string with
/// <c>[AsParameters]</c> — the parameterless constructor applies the
/// default limit.
///
/// <para>
/// There is no built-in cap — <see cref="Limit"/> is only clamped to a
/// minimum of 1. If you need a maximum bound, implement it in your own
/// query type.
/// </para>
/// </summary>
public sealed class DefaultCursorQuery : IPaginatedQuery
{
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

    /// <summary>Items per page, clamped to a minimum of 1.</summary>
    public int Limit
    {
        get => _limit;
        set => _limit = Math.Max(1, value);
    }
}
