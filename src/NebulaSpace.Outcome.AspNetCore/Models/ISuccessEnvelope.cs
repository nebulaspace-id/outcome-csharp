using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Typed contract for a success envelope that knows the payload type at compile time.
/// Implement it on an <b>open generic</b> type when you need Swagger to document
/// <c>Data: Order</c> rather than <c>Data: object</c> per endpoint.
///
/// <para>
/// Register with <c>options.Success.SetResponse(typeof(ApiSuccess&lt;&gt;))</c> — the
/// library closes <c>T</c> with the payload's runtime type per request.
/// <b>This uses <c>MakeGenericType</c> and is NOT AOT-safe.</b>
///
/// For paginated responses, use a 2‑param generic implementing this interface so
/// the second parameter carries the concrete pagination type
/// (<see cref="Pagination.OffsetInfo"/> or <see cref="Pagination.CursorInfo"/>):
/// <c>options.Success.SetResponse(typeof(ApiSuccess&lt;,&gt;))</c>.
/// </para>
/// </summary>
/// <typeparam name="T">The payload type (the <c>data</c> the envelope wraps).</typeparam>
public interface ISuccessEnvelope<in T>
{
    /// <summary>
    /// Builds the envelope instance for one response.
    /// </summary>
    /// <param name="data">The payload — already the closed <typeparamref name="T"/> at call time.</param>
    /// <param name="pagination">
    /// Pagination metadata when the response is paginated, else <c>null</c>.
    /// Either <see cref="Pagination.OffsetInfo"/> (offset) or <see cref="Pagination.CursorInfo"/> (cursor).
    /// </param>
    /// <param name="httpContext">The current HTTP request.</param>
    /// <param name="statusCode">The HTTP status the response will use (<c>200</c> for <c>Ok</c>, <c>201</c> for <c>Created</c>).</param>
    static abstract ISuccessEnvelope<T> From(T data, IPaginationDetails? pagination, HttpContext httpContext, int statusCode);
}
