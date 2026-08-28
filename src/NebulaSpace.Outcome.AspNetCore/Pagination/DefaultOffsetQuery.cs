using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Incoming offset-pagination parameters. Bound from the query string with
/// <c>[AsParameters]</c>.
/// </summary>
public sealed class DefaultOffsetQuery : IPaginatedQuery
{
    /// <summary>
    /// Maximum page number. Set from <c>options.Pagination.MaxPage</c> at startup.
    /// Default <see cref="int.MaxValue"/> — uncapped unless the app sets a value.
    /// </summary>
    /// <remarks>
    /// A <b>field</b>, not a settable property: a <c>public static</c> property with a public
    /// setter makes ASP.NET Core's <c>RequestDelegateFactory</c> try to bind it as a query
    /// parameter and emit invalid IL (<see cref="System.InvalidProgramException"/>).
    /// </remarks>
    public static int MaxPage = int.MaxValue;

    /// <summary>
    /// Maximum items per page. Set from <c>options.Pagination.MaxLimit</c> at startup.
    /// Default <see cref="int.MaxValue"/> — uncapped unless the app sets a value.
    /// </summary>
    /// <remarks>
    /// A <b>field</b>, not a settable property — see <see cref="MaxPage"/>.
    /// </remarks>
    public static int MaxLimit = int.MaxValue;

    private int _page = 1;
    private int _limit = 10;

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
    /// The 0-based offset of the first item in this page, i.e. <c>(Page - 1) * Limit</c>.
    /// </summary>
    [BindNever]
    public int Offset => (Page - 1) * Limit;

    /// <inheritdoc />
    public static void Configure(Options.PaginationOptions options)
    {
        MaxPage  = Math.Max(1, options.MaxPage);
        MaxLimit = Math.Max(1, options.MaxLimit);
    }
}
