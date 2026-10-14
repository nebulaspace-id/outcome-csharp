using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Incoming cursor-pagination parameters — bound from the query string with
/// <c>[AsParameters]</c>.  This is a custom implementation built by the app,
/// not the library's built-in <c>DefaultCursorQuery</c>.
///
/// <para>
/// Demonstrates how users can build their own cursor pagination query with an
/// app-specific cap. The bound is written directly on this type as a
/// <b>static field</b> — there is no options plumbing to configure it.
/// </para>
/// </summary>
public sealed class CursorQuery : IPaginatedQuery
{
    /// <summary>Maximum items per page a client can request.</summary>
    /// <remarks>
    /// A <b>field</b>, not a settable property — see <see cref="DefaultCursorQuery"/>.
    /// </remarks>
    public static int MaxLimit = 50;

    private string? _cursor;
    private int _limit = 10;

    /// <summary>
    /// Parameterless constructor for <c>[AsParameters]</c> binding — applies the
    /// default limit.
    /// </summary>
    public CursorQuery() { }

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
