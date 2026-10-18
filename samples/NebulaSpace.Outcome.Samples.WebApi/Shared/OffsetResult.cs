namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// A single page of items together with the total count across all pages. The service
/// returns this and the endpoint, which already has the incoming <see cref="OffsetQuery"/>,
/// builds the response <see cref="Meta"/> from both — separating the query intent
/// (page/limit) from the raw data the service reports (items + count).
/// </summary>
/// <typeparam name="T">The type of each item in the page.</typeparam>
public sealed class OffsetResult<T>
{
    /// <summary>Items in the current page (never <c>null</c>).</summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>Total count of items across all pages.</summary>
    public long TotalCount { get; init; }
}
