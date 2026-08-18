namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Incoming cursor-pagination parameters. Bound from the query string with
/// <c>[AsParameters]</c> — the parameterless constructor applies the default limits.
///
/// <para>
/// The static default is set from <see cref="Options.PaginationOptions"/> during
/// <c>AddOutcome</c> — call <c>options.Pagination.MaxLimit</c> to configure.
/// Values are clamped (never reset).
/// </para>
/// </summary>
public sealed class CursorQuery : IPaginatedQuery
{
    /// <summary>
    /// Maximum items per page. Set from <c>options.Pagination.MaxLimit</c> at startup.
    /// Default <see cref="int.MaxValue"/> — uncapped unless the app sets a value.
    /// </summary>
    /// <remarks>
    /// A <b>field</b>, not a settable property: a <c>public static</c> property with a public
    /// setter makes ASP.NET Core's <c>RequestDelegateFactory</c> try to bind it as a query
    /// parameter and emit invalid IL (<see cref="System.InvalidProgramException"/>).
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
    public static void Configure(Options.PaginationOptions options)
    {
        MaxLimit = Math.Max(1, options.MaxLimit);
    }
}
