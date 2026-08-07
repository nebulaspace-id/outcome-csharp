using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Contract for a typed success envelope. Implement it on the type the app wants
/// serialized for successful responses; the <c>From</c> factory builds the per-request
/// body from the payload, optional pagination, the current request, and the HTTP status
/// the response will use (<c>200</c> for <c>Ok</c>, <c>201</c> for <c>Created</c>).
/// <para>
/// Register an open generic implementation with
/// <c>options.SuccessEnvelope.Set(typeof(MySuccess&lt;&gt;))</c> — the library closes the
/// type parameter with the payload's type per request and invokes <c>From</c>. Because the
/// type parameter is also the payload type, <c>.Produces&lt;MySuccess&lt;T&gt;&gt;()</c>
/// documents the exact success shape, with the payload substituted for <c>T</c>.
/// </para>
/// </summary>
/// <typeparam name="T">The payload type (the <c>data</c> the envelope wraps).</typeparam>
public interface ISuccessEnvelope<T>
{
    /// <summary>Builds the envelope instance for one response.</summary>
    /// <param name="data">The payload — already the closed <typeparamref name="T"/> at call time.</param>
    /// <param name="pagination">Pagination metadata when the response is paginated, else <c>null</c>.</param>
    /// <param name="httpContext">The current request.</param>
    /// <param name="statusCode">The HTTP status the response will use.</param>
    static abstract ISuccessEnvelope<T> From(T data, PageInfo? pagination, HttpContext httpContext, int statusCode);
}
