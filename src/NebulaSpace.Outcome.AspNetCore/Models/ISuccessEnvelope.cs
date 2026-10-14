using Microsoft.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Typed contract for a success envelope that knows the payload type at compile time.
/// Implement it on an <b>open generic</b> type when you need Swagger to document
/// <c>Data: Order</c> rather than <c>Data: object</c> per endpoint.
///
/// <para>
/// Register with <c>options.Success.SetResponse(typeof(ApiSuccess&lt;&gt;))</c> — the
/// library closes <c>T</c> with the payload's runtime type per request.
/// </para>
/// </summary>
/// <typeparam name="T">The payload type (the <c>data</c> the envelope wraps).</typeparam>
public interface ISuccessEnvelope<in T>
{
    /// <summary>
    /// Builds the envelope instance for one response.
    /// </summary>
    /// <param name="data">The payload — already the closed <typeparamref name="T"/> at call time.</param>
    /// <param name="meta">
    /// The metadata the app attached to this response, or <c>null</c>. Opaque to the
    /// library: <see cref="object"/> so any app type can be carried, and System.Text.Json
    /// serializes the runtime type's full shape.
    /// </param>
    /// <param name="httpContext">The current HTTP request.</param>
    /// <param name="statusCode">The HTTP status the response will use (<c>200</c> for <c>Ok</c>, <c>201</c> for <c>Created</c>).</param>
    static abstract ISuccessEnvelope<T> From(T data, object? meta, HttpContext httpContext, int statusCode);
}
