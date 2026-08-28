namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// A single page of items together with the total count across all pages. The service
/// returns this and the endpoint, which already has the incoming <see cref="DefaultOffsetQuery"/>,
/// constructs the <see cref="DefaultOffsetInfo"/> metadata from it — separating the query intent
/// (page/limit) from the raw data the service reports (items + count).
///
/// <para>
/// Use <see cref="DefaultOffsetInfo.Create(DefaultOffsetQuery,int)"/> to build the metadata from this
/// result in one call.
/// </para>
/// </summary>
/// <typeparam name="T">The type of each item in the page.</typeparam>
public sealed class DefaultOffsetResult<T>
{
    /// <summary>Items in the current page (never <c>null</c>).</summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>Total count of items across all pages.</summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// Builds <see cref="DefaultOffsetInfo"/> from this result and the incoming query —
    /// convenience over <c>DefaultOffsetInfo.Create(query, (int)TotalCount)</c>.
    /// </summary>
    /// <param name="query">The incoming offset-pagination request.</param>
    public DefaultOffsetInfo ToOffsetInfo(DefaultOffsetQuery query) =>
        DefaultOffsetInfo.Create(query, (int)TotalCount);
}
