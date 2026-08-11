using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Incoming cursor-pagination parameters. Bound from the query string with
/// <c>[AsParameters]</c> — the parameterless constructor applies the default limits, and
/// <c>[BindNever]</c> on <see cref="MaxLimit"/> prevents clients from injecting their
/// own cap.
///
/// <para>
/// The static default is set from <see cref="Options.PaginationOptions"/> during
/// <c>AddOutcome</c> — call <c>options.Pagination.MaxLimit</c> to configure.
/// Values are clamped (never reset).
/// </para>
/// </summary>
public sealed class CursorQuery
{
    /// <summary>
    /// Default maximum items per page, overridden at startup from
    /// <c>options.Pagination.MaxLimit</c>. Default 50.
    /// </summary>
    public static int DefaultMaxLimit { get; set; } = 50;

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

    /// <summary>
    /// Maximum items per page allowed by this query. Backed by
    /// <see cref="DefaultMaxLimit"/> unless set explicitly via an init-only property.
    /// <c>[BindNever]</c> — never accepted from the client.
    /// </summary>
    [BindNever]
    public int MaxLimit { get; init; } = DefaultMaxLimit;
}
