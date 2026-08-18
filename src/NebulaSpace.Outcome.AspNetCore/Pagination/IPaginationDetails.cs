namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Marker interface for pagination metadata — offset, cursor, keyset, or custom.
/// Implementations define their own properties; the pipeline resolves the concrete
/// type via <see cref="SuccessResponse{TData, TPage}"/> so System.Text.Json and
/// Swagger serialize the full shape, not just the interface members.
///
/// <para>
/// Implement this on a class with whatever pagination fields your strategy needs
/// (page/limit/totalPages, nextCursor/previousCursor, nextKey/hasMore, etc.) and
/// pass it to <c>IHttpResultFactory.Ok&lt;T&gt;(T, IPaginationDetails)</c>.
/// </para>
/// </summary>
public interface IPaginationDetails { }
