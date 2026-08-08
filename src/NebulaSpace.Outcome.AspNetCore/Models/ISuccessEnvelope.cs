using Microsoft.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Contract for a typed success envelope. Implement it on a non‑generic type the app
/// wants serialized for every successful response; the <c>From</c> factory builds the
/// per‑request body from the response intent (<see cref="SuccessDetails"/>) and the
/// current request.
///
/// <para>
/// Register with <c>options.Success.SetResponse&lt;MySuccess&gt;()</c> — the library
/// dispatches through the static abstract <c>From</c>, so it is <b>AOT‑safe, zero‑reflection</b>,
/// and compile‑time safe. Because the serialized body <em>is</em> that type,
/// <c>.Produces&lt;MySuccess&gt;()</c> documents the exact success shape.
/// </para>
///
/// <para>
/// Define one envelope and register it once. Every <c>http.Ok(data)</c>,
/// <c>http.Created(data, idSelector)</c>, and <c>outcome.ToHttpResult(...)</c> that
/// resolves to a success renders through it.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public sealed class SuccessResponse : ISuccessEnvelope
/// {
///     public int StatusCode { get; init; }
///     public object? Data { get; init; }
///     public PageInfo? Pagination { get; init; }
///     public string? TraceId { get; init; }
///
///     public static ISuccessEnvelope From(SuccessDetails details, HttpContext http) =>
///         new SuccessResponse
///         {
///             StatusCode = details.StatusCode,
///             Data = details.Data,
///             Pagination = details.Pagination,
///             TraceId = http.TraceIdentifier,
///         };
/// }
/// </code>
/// </example>
public interface ISuccessEnvelope
{
    /// <summary>
    /// Builds the envelope instance for one response.
    /// </summary>
    /// <param name="details">The response's intent: payload, pagination, and status code.</param>
    /// <param name="httpContext">The current HTTP request.</param>
    static abstract ISuccessEnvelope From(SuccessDetails details, HttpContext httpContext);
}

/// <summary>
/// Typed contract for a success envelope that knows the payload type at compile time.
/// Implement it on an <b>open generic</b> type when you need Swagger to document
/// <c>Data: Order</c> rather than <c>Data: object</c> per endpoint.
///
/// <para>
/// Register with <c>options.Success.SetResponse(typeof(ApiSuccess&lt;&gt;))</c> — the
/// library closes <c>T</c> with the payload's runtime type per request.
/// <b>This path uses <c>MakeGenericType</c> and is NOT AOT‑safe.</b> For NativeAOT,
/// use the non‑generic <see cref="ISuccessEnvelope"/> plus
/// <c>options.Success.SetResponse&lt;MySuccess&gt;()</c>.
/// </para>
/// </summary>
/// <typeparam name="T">The payload type (the <c>data</c> the envelope wraps).</typeparam>
public interface ISuccessEnvelope<T>
{
    /// <summary>
    /// Builds the envelope instance for one response.
    /// </summary>
    /// <param name="data">The payload — already the closed <typeparamref name="T"/> at call time.</param>
    /// <param name="pagination">Pagination metadata when the response is paginated, else <c>null</c>.</param>
    /// <param name="httpContext">The current HTTP request.</param>
    /// <param name="statusCode">The HTTP status the response will use (<c>200</c> for <c>Ok</c>, <c>201</c> for <c>Created</c>).</param>
    static abstract ISuccessEnvelope<T> From(T data, Pagination.PageInfo? pagination, HttpContext httpContext, int statusCode);
}
