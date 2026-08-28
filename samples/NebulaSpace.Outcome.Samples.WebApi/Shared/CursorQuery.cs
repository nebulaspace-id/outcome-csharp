using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Incoming cursor-pagination parameters — bound from the query string with
/// <c>[AsParameters]</c>.  This is a custom implementation built by the app,
/// not the library's built-in <c>DefaultCursorQuery</c>.
///
/// <para>
/// Demonstrates how users can build their own cursor pagination query with
/// app-specific caps and naming while still leveraging the library's
/// <see cref="IPaginatedQuery"/> auto-discovery for <see cref="PaginationOptions"/>
/// propagation.
/// </para>
/// </summary>
public sealed class CursorQuery : IPaginatedQuery
{
    /// <summary>
    /// Maximum items per page. Set from <c>options.Pagination.MaxLimit</c> at startup.
    /// Default <see cref="int.MaxValue"/> — uncapped unless the app sets a value.
    /// </summary>
    /// <remarks>
    /// A <b>field</b>, not a settable property — see <see cref="DefaultCursorQuery.MaxLimit"/>.
    /// </remarks>
    public static int MaxLimit = int.MaxValue;

    private string? _cursor;
    private int _limit = 10;

    /// <summary>
    /// Parameterless constructor for <c>[AsParameters]</c> binding — applies the static
    /// default.
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

    /// <inheritdoc />
    public static void Configure(PaginationOptions options)
    {
        MaxLimit = Math.Max(1, options.MaxLimit);
    }
}
