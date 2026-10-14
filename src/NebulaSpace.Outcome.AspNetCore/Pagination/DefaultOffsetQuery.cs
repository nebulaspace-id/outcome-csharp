using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Incoming offset-pagination parameters. Bound from the query string with
/// <c>[AsParameters]</c>.
///
/// <para>
/// There are no built-in caps — <see cref="Page"/> and <see cref="Limit"/>
/// are only clamped to a minimum of 1. If you need maximum bounds,
/// implement them in your own query type.
/// </para>
/// </summary>
public sealed class DefaultOffsetQuery : IPaginatedQuery
{
    private int _page = 1;
    private int _limit = 10;

    /// <summary>1-based page number, clamped to a minimum of 1.</summary>
    public int Page
    {
        get => _page;
        set => _page = Math.Max(1, value);
    }

    /// <summary>Items per page, clamped to a minimum of 1.</summary>
    public int Limit
    {
        get => _limit;
        set => _limit = Math.Max(1, value);
    }

    /// <summary>
    /// The 0-based offset of the first item in this page, i.e. <c>(Page - 1) * Limit</c>.
    /// </summary>
    [BindNever]
    public int Offset => (Page - 1) * Limit;
}
