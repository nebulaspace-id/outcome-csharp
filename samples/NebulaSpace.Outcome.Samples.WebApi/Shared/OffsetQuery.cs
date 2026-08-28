using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Incoming offset-pagination parameters — bound from the query string with
/// <c>[AsParameters]</c>.  This is a custom implementation built by the app,
/// not the library's built-in <c>DefaultOffsetQuery</c>.
///
/// <para>
/// Demonstrates how users can build their own offset pagination query with
/// app-specific caps and naming while still leveraging the library's
/// <see cref="IPaginatedQuery"/> auto-discovery for <see cref="PaginationOptions"/>
/// propagation.
/// </para>
/// </summary>
public sealed class OffsetQuery : IPaginatedQuery
{
    /// <summary>
    /// Maximum page number. Set from <c>options.Pagination.MaxPage</c> at startup.
    /// Default <see cref="int.MaxValue"/> — uncapped unless the app sets a value.
    /// </summary>
    /// <remarks>
    /// A <b>field</b>, not a settable property — see <see cref="DefaultOffsetQuery.MaxPage"/>.
    /// </remarks>
    public static int MaxPage = int.MaxValue;

    /// <summary>
    /// Maximum items per page. Set from <c>options.Pagination.MaxLimit</c> at startup.
    /// Default <see cref="int.MaxValue"/> — uncapped unless the app sets a value.
    /// </summary>
    /// <remarks>
    /// A <b>field</b>, not a settable property — see <see cref="DefaultOffsetQuery.MaxPage"/>.
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
    public int Offset => (Page - 1) * Limit;

    /// <inheritdoc />
    public static void Configure(PaginationOptions options)
    {
        MaxPage  = Math.Max(1, options.MaxPage);
        MaxLimit = Math.Max(1, options.MaxLimit);
    }
}
