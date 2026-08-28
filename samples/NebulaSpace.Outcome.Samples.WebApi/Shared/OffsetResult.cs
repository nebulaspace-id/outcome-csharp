namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// A single page of items together with the total count across all pages. The service
/// returns this and the endpoint, which already has the incoming <see cref="OffsetQuery"/>,
/// constructs the <see cref="OffsetInfo"/> metadata from it — separating the query intent
/// (page/limit) from the raw data the service reports (items + count).
///
/// <para>
/// Use <see cref="ToOffsetInfo"/> to build the metadata from this result in one call.
/// </para>
/// </summary>
/// <typeparam name="T">The type of each item in the page.</typeparam>
public sealed class OffsetResult<T>
{
    /// <summary>Items in the current page (never <c>null</c>).</summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>Total count of items across all pages.</summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// Builds <see cref="OffsetInfo"/> from this result and the incoming query —
    /// convenience over <c>OffsetInfo.Create(query, (int)TotalCount)</c>.
    /// </summary>
    /// <param name="query">The incoming offset-pagination request.</param>
    public OffsetInfo ToOffsetInfo(OffsetQuery query) =>
        OffsetInfo.Create(query, (int)TotalCount);
}
