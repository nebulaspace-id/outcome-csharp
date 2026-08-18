using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Incoming keyset-pagination parameters — bound from the query string with
/// <c>[AsParameters]</c>.  Uses the product id as the opaque key; <see cref="AfterKey"/>
/// is the <c>Guid</c> of the last item on the previous page (omit for the first page).
///
/// <para>
/// Unlike offset pagination, keyset pages are stable under inserts/deletes and scale
/// to arbitrarily large datasets because they never skip rows.
/// </para>
/// </summary>
public sealed class KeysetQuery : IPaginatedQuery
{
    /// <summary>
    /// Maximum items per page. Set from <c>options.Pagination.MaxLimit</c> at startup.
    /// Default <see cref="int.MaxValue"/> — uncapped unless the app sets a value.
    /// </summary>
    /// <remarks>
    /// A <b>field</b>, not a settable property — see <see cref="OffsetQuery.MaxPage"/>.
    /// </remarks>
    public static int MaxLimit = int.MaxValue;

    private int _limit = 10;

    /// <summary>
    /// Opaque key pointing to the last item on the previous page — the response
    /// includes this as <c>previousKey</c> so the client can navigate backwards.
    /// <c>null</c> for the first page.
    /// </summary>
    public string? AfterKey { get; set; }

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
