using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Incoming offset-pagination parameters. Bound from the query string with
/// <c>[AsParameters]</c> — the parameterless constructor applies the default limits, and
/// <c>[BindNever]</c> on <see cref="MaxPage"/> and <see cref="MaxLimit"/> prevents
/// clients from injecting their own caps.
///
/// <para>
/// The static defaults are set from <see cref="Options.PaginationOptions"/> during
/// <c>AddOutcome</c> — call <c>options.Pagination.MaxPage/MaxLimit</c> to configure.
/// Values are clamped (never reset), so <c>?page=999</c> becomes the last page allowed
/// rather than page 1.
/// </para>
/// </summary>
public sealed class OffsetQuery
{
    /// <summary>
    /// Default maximum page number, overridden at startup from
    /// <c>options.Pagination.MaxPage</c>. Default 25.
    /// </summary>
    public static int DefaultMaxPage { get; set; } = 25;

    /// <summary>
    /// Default maximum items per page, overridden at startup from
    /// <c>options.Pagination.MaxLimit</c>. Default 50.
    /// </summary>
    public static int DefaultMaxLimit { get; set; } = 50;

    private int _page = 1;
    private int _limit = 10;

    /// <summary>
    /// Parameterless constructor for <c>[AsParameters]</c> binding — applies the static
    /// defaults.
    /// </summary>
    public OffsetQuery() { }

    /// <summary>1-based page number, clamped to <c>1..MaxPage</c>.</summary>
    public int Page
    {
        get => _page;
        set => _page = Math.Clamp(value, 1, MaxPage);
    }

    /// <summary>Items per page, clamped to <c>1..MaxLimit</c>.</summary>
    public int Limit
    {
        get => _limit;
        set => _limit = Math.Clamp(value, 1, MaxLimit);
    }

    /// <summary>
    /// Maximum page number allowed by this query. Backed by
    /// <see cref="DefaultMaxPage"/> unless set explicitly via an init-only property.
    /// <c>[BindNever]</c> — never accepted from the client.
    /// </summary>
    [BindNever]
    public int MaxPage { get; init; } = DefaultMaxPage;

    /// <summary>
    /// Maximum items per page allowed by this query. Backed by
    /// <see cref="DefaultMaxLimit"/> unless set explicitly via an init-only property.
    /// <c>[BindNever]</c> — never accepted from the client.
    /// </summary>
    [BindNever]
    public int MaxLimit { get; init; } = DefaultMaxLimit;

    /// <summary>
    /// The 0-based offset of the first item in this page, i.e. <c>(Page - 1) * Limit</c>.
    /// Convenience for direct use with <c>Skip()</c>.
    /// </summary>
    public int Offset => (Page - 1) * Limit;
}
