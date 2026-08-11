namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Shared pagination metadata contract for all pagination strategies — offset, cursor,
/// keyset, or custom. Implementations define their own properties beyond
/// <see cref="Type"/>; the pipeline resolves the concrete type via
/// <see cref="SuccessResponse{TData, TPage}"/> so System.Text.Json and Swagger
/// serialize the full shape, not just this interface's single member.
/// </summary>
public interface IPaginationDetails
{
    /// <summary>
    /// Discriminator string identifying the pagination strategy.
    /// Built-in values: <c>"offset"</c> for <see cref="OffsetInfo"/>,
    /// <c>"cursor"</c> for <see cref="CursorInfo"/>.
    /// Custom implementations should pick a unique, stable name.
    /// </summary>
    string Type { get; }
}
