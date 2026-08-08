using Microsoft.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Contract for a typed error envelope. Implement it on the type the app wants serialized
/// for every failed response; the <c>From</c> factory builds the per‑request body from a
/// failure's HTTP intent (<see cref="FailureDetails"/>) and the current request.
///
/// <para>
/// Register with <c>options.Error.SetResponse&lt;MyError&gt;()</c> — the library dispatches
/// through the static abstract <c>From</c>, so it is <b>AOT‑safe, zero‑reflection</b>,
/// and compile‑time safe. Because the serialized body <em>is</em> that type,
/// <c>.Produces&lt;MyError&gt;(404)</c> documents the exact error shape, and per‑request
/// fields (<c>traceId</c>, <c>path</c>, custom) are real C# properties, not runtime
/// dictionary keys.
/// </para>
///
/// <para>
/// Define one envelope and register it once. Expected failures, <c>MapFailure</c> results,
/// mapped exceptions, and unhandled exceptions all reduce to <see cref="FailureDetails"/>
/// and render through this one shape.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public sealed class ErrorResponse : IErrorEnvelope
/// {
///     public int? StatusCode { get; init; }
///     public string? Code { get; init; }
///     public string? Message { get; init; }
///     public string? TraceId { get; init; }
///     public IReadOnlyDictionary&lt;string, string[]&gt;? Errors { get; init; }
///
///     public static IErrorEnvelope From(FailureDetails details, HttpContext ctx) =>
///         new ErrorResponse
///         {
///             StatusCode = details.StatusCode,
///             Code = details.Code,
///             Message = details.Message,
///             TraceId = ctx.TraceIdentifier,
///             Errors = details.Errors,
///         };
/// }
/// </code>
/// </example>
public interface IErrorEnvelope
{
    /// <summary>
    /// Builds the envelope instance for one failed response.
    /// </summary>
    /// <param name="details">The failure's HTTP intent (status, message, code, field errors).</param>
    /// <param name="httpContext">The current HTTP request.</param>
    static abstract IErrorEnvelope From(FailureDetails details, HttpContext httpContext);
}
