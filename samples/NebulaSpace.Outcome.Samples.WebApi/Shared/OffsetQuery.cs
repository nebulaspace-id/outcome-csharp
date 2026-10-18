using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Incoming offset-pagination parameters — bound from the query string with
/// <c>[AsParameters]</c>.  This is a custom implementation built by the app,
/// not the library's built-in <c>DefaultOffsetQuery</c>.
///
/// <para>
/// Demonstrates how users can build their own offset pagination query with
/// app-specific caps. The bounds are written directly on this type as
/// <b>static fields</b> — there is no options plumbing to configure them.
/// </para>
/// </summary>
public sealed class OffsetQuery : IPaginatedQuery
{
    /// <summary>Maximum page number a client can request.</summary>
    /// <remarks>
    /// A <b>field</b>, not a settable property — see <see cref="DefaultOffsetQuery"/>.
    /// </remarks>
    private const int MaxPage = 25;

    /// <summary>Maximum items per page a client can request.</summary>
    /// <remarks>
    /// A <b>field</b>, not a settable property — see <see cref="DefaultOffsetQuery"/>.
    /// </remarks>
    private const int MaxLimit = 50;

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
}
