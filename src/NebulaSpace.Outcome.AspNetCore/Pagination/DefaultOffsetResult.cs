namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// A single page of items together with the total count across all pages. The service
/// returns this and the endpoint, which already has the incoming <see cref="DefaultOffsetQuery"/>,
/// builds the <see cref="DefaultOffsetPagination"/> metadata from both — separating the query
/// intent (page/limit) from the raw data the service reports (items + count).
///
/// <para>
/// Use <see cref="DefaultOffsetPagination.Create{T}"/> to build the metadata from this
/// result in one call.
/// </para>
/// </summary>
/// <typeparam name="T">The type of each item in the page.</typeparam>
public sealed class DefaultOffsetResult<T>
{
    /// <summary>Items in the current page (never <c>null</c>).</summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>Total count of items across all pages.</summary>
    public long TotalCount { get; init; }
}
