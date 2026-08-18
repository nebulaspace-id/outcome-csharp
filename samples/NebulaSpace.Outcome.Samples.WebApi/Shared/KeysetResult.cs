namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// A single page from a keyset-paginated query, together with opaque navigation keys.
/// The service returns this and the endpoint builds <see cref="KeysetInfo"/> from it
/// via <see cref="ToKeysetInfo"/>.
/// </summary>
/// <typeparam name="T">The type of each item in the page.</typeparam>
public sealed class KeysetResult<T>
{
    /// <summary>Items in the current page (never <c>null</c>).</summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>
    /// Key of the last item on this page — the client passes it back as
    /// <c>afterKey</c> to fetch the next page. <c>null</c> when there are no more items.
    /// </summary>
    public string? NextKey { get; set; }

    /// <summary>
    /// Key of the first item on this page — the client passes it back to navigate
    /// backwards. <c>null</c> on the first page.
    /// </summary>
    public string? PreviousKey { get; set; }

    /// <summary>Whether at least one more page of data exists after this one.</summary>
    public bool HasMore { get; set; }

    /// <summary>Total items across all pages, when known.</summary>
    public long? TotalItems { get; set; }

    /// <summary>
    /// Builds <see cref="KeysetInfo"/> from this result — convenience over
    /// constructing the metadata manually.
    /// </summary>
    public KeysetInfo ToKeysetInfo() => new()
    {
        NextKey = NextKey,
        PreviousKey = PreviousKey,
        HasMore = HasMore,
        TotalItems = TotalItems,
    };
}
