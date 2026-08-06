namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// A single page of items together with the total count across all pages.
/// The page number and page size live on the incoming <see cref="PageQuery"/>,
/// so this type carries only the raw count and lets callers build their own
/// <see cref="Pagination"/> metadata.
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public long TotalCount { get; set; }
}
