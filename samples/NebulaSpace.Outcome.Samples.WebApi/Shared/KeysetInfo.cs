using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Custom keyset-pagination metadata sent in the response body. Implements
/// <see cref="IPaginationDetails"/> so the library resolves the concrete type at
/// runtime and System.Text.Json / Swagger serialize the full shape — <c>nextKey</c>,
/// <c>previousKey</c>, <c>hasMore</c>, and <c>totalItems</c>.
///
/// <para>
/// Build via <see cref="KeysetResult{T}.ToKeysetInfo"/> — one line from the endpoint.
/// </para>
/// </summary>
public sealed class KeysetInfo : IPaginationDetails
{
    /// <summary>
    /// Key of the last item on this page. The client passes it back as
    /// <c>afterKey</c> to fetch the next page. <c>null</c> when there are no more items.
    /// </summary>
    public string? NextKey { get; set; }

    /// <summary>
    /// Key of the first item on this page. The client passes it back to navigate
    /// backwards. <c>null</c> on the first page.
    /// </summary>
    public string? PreviousKey { get; set; }

    /// <summary>Whether at least one more page of data exists after this one.</summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// Total items across all pages, when the service provides it.
    /// <c>null</c> when unknown (omitted from JSON via
    /// <c>JsonIgnoreCondition.WhenWritingNull</c>).
    /// </summary>
    public long? TotalItems { get; set; }
}
