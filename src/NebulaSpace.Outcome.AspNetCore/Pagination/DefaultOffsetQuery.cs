using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Incoming offset-pagination parameters. Bound from the query string with
/// <c>[AsParameters]</c>.
///
/// <para>
/// Values are clamped to safe bounds so a hostile query cannot exhaust server resources:
/// <see cref="Page"/> to <c>1..<see cref="MaxPage"/></c> and
/// <see cref="Limit"/> to <c>1..<see cref="MaxLimit"/></c>. The caps are constants —
/// custom query types can apply their own policy.
/// </para>
/// </summary>
public sealed class DefaultOffsetQuery : IPaginatedQuery
{
    /// <summary>Maximum page number a client can request. Defaults to <c>1,000,000</c>.</summary>
    public const int MaxPage = 1_000_000;

    /// <summary>Maximum items per page a client can request. Defaults to <c>100</c>.</summary>
    public const int MaxLimit = 100;

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
    /// The multiplication is performed in 64-bit arithmetic so it cannot overflow.
    /// </summary>
    [BindNever]
    public int Offset => (int)Math.Min((long)(Page - 1) * Limit, int.MaxValue);
}
