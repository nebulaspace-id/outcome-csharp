using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

// ──────────────────────────────────────────────────────────────────────────
// Custom offset-pagination type — demo of IPaginationDetails extensibility.
//
// Field names are DIFFERENT from the library's built-in OffsetInfo
// (Page → PageNumber, Limit → PageSize, TotalItems → TotalCount).
// STJ and Swagger serialize the full concrete shape because TPage is
// resolved via pagination?.GetType() in SuccessOptions.PaginationFactory.
//
// No separate registration needed — the one-time call
//   options.Success.SetResponse(typeof(SuccessResponse<,>))
// covers every IPaginationDetails implementation automatically.
// ──────────────────────────────────────────────────────────────────────────

/// <summary>
/// Custom offset-pagination metadata with application-specific field names.
/// Implements <see cref="IPaginationDetails"/> so it flows through the same
/// <c>SuccessResponse&lt;TData, TPage&gt;</c> pipeline as the built-in types.
/// </summary>
public sealed class OffsetPage : IPaginationDetails
{
    /// <summary>Discriminator — always <c>"offset"</c> for this style.</summary>
    public string Type => "offset";

    /// <summary>1‑based page number.</summary>
    public int PageNumber { get; init; }

    /// <summary>Items per page the server returned.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages { get; init; }

    /// <summary>Total items across all pages, or <c>null</c> when unknown.</summary>
    public long? TotalCount { get; init; }

    /// <summary>Whether there is at least one more page of data after this one.</summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>
    /// Creates <see cref="OffsetPage"/> from a <see cref="OffsetQuery"/> and total item
    /// count — the custom equivalent of <c>OffsetInfo.Create(query, totalItems)</c>.
    /// </summary>
    public static OffsetPage Create(OffsetQuery query, int totalCount) => new()
    {
        PageNumber = query.Page,
        PageSize = query.Limit,
        TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / query.Limit),
        TotalCount = totalCount,
    };
}
