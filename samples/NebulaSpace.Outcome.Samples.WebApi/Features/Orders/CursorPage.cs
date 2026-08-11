using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

// ──────────────────────────────────────────────────────────────────────────
// Custom cursor-pagination type — demo of IPaginationDetails extensibility.
//
// Field names are DIFFERENT from the library's built-in CursorInfo
// (PreviousCursor → Before, NextCursor → After, HasMore → HasNextPage,
//  TotalItems → TotalCount).
// STJ and Swagger serialize the full concrete shape because TPage is
// resolved via pagination?.GetType() in SuccessOptions.PaginationFactory.
//
// No separate registration needed — the one-time call
//   options.Success.SetResponse(typeof(SuccessResponse<,>))
// covers every IPaginationDetails implementation automatically.
// ──────────────────────────────────────────────────────────────────────────

/// <summary>
/// Custom cursor-pagination metadata with application-specific field names.
/// Implements <see cref="IPaginationDetails"/> so it flows through the same
/// <c>SuccessResponse&lt;TData, TPage&gt;</c> pipeline as the built-in types.
/// </summary>
public sealed class CursorPage : IPaginationDetails
{
    /// <summary>Discriminator — always <c>"cursor"</c> for this style.</summary>
    public string Type => "cursor";

    /// <summary>Opaque cursor for the previous page. <c>null</c> on the first page.</summary>
    public string? Before { get; init; }

    /// <summary>Opaque cursor for the next page. <c>null</c> on the last page.</summary>
    public string? After { get; init; }

    /// <summary>Whether there is at least one more page of data after this one.</summary>
    public bool HasNextPage { get; init; }

    /// <summary>Total items across all pages, or <c>null</c> when unknown.</summary>
    public long? TotalCount { get; init; }

    /// <summary>
    /// Creates <see cref="CursorPage"/> from a <see cref="CursorQuery"/> and the service
    /// result — the custom equivalent of <c>CursorInfo.Create(query, result)</c>.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    public static CursorPage Create<T>(CursorQuery query, CursorResult<T> result) => new()
    {
        Before = result.PreviousCursor,
        After = result.NextCursor,
        HasNextPage = result.HasMore,
        TotalCount = result.TotalCount,
    };
}
