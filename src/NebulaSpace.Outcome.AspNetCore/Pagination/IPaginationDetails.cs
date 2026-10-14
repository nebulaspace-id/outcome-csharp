namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Marker interface for pagination metadata — offset, cursor, keyset, or custom.
/// Implementations define their own properties.
///
/// <para>
/// Implement this on a class with whatever pagination fields your strategy needs
/// (page/limit/totalPages, nextCursor/previousCursor, nextKey/hasMore, etc.) and
/// pass it — as part of the app's response metadata — to
/// <c>IHttpResultFactory.Ok&lt;T&gt;(T, object)</c>. Because that parameter is
/// typed <see cref="object"/>, System.Text.Json serializes the pagination type's
/// runtime shape, not just this empty marker.
/// </para>
/// </summary>
public interface IPaginationDetails { }
