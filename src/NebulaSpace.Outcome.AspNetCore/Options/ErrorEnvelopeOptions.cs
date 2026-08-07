using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Configuration for the error envelope: the TYPE and how it is built from a failure's
/// HTTP intent plus the current request. The envelope can be any class — it inherits
/// nothing from the library. Because the serialized body IS that type, OpenAPI
/// (<c>.Produces&lt;TEnvelope&gt;()</c>) documents the exact error shape, and per-request
/// fields (<c>traceId</c>, <c>path</c>, custom) are real C# properties, not runtime
/// dictionary keys.
///
/// By default no envelope is configured and the library falls back to the clean RFC 7807
/// <see cref="DefaultProblemDetails"/> (title/status/detail only).
/// </summary>
public sealed class ErrorEnvelopeOptions
{
    /// <summary>
    /// Builds a concrete envelope instance from the failure's HTTP intent and the current
    /// request. <c>null</c> until <see cref="Set{TEnvelope}"/> is called, after which
    /// <see cref="ErrorEnvelopeResult"/> uses it instead of <see cref="DefaultProblemDetails"/>.
    /// </summary>
    internal Func<FailureDetails, HttpContext, object>? Factory { get; private set; }

    /// <summary>
    /// Sets the error envelope: a concrete type <typeparamref name="TEnvelope"/>,
    /// built by <paramref name="factory"/> from the failure's HTTP intent
    /// (<see cref="FailureDetails"/>) and the current <see cref="HttpContext"/>.
    /// The returned instance is serialized as-is (HTTP status written separately from
    /// <c>FailureDetails.StatusCode</c>). Example — pass a method group directly, the
    /// type is inferred:
    /// <code>
    /// options.ErrorEnvelope.Set(ErrorResponse.From);
    /// </code>
    /// or inline:
    /// <code>
    /// options.ErrorEnvelope.Set((details, ctx) => new ErrorResponse
    /// {
    ///     Code = details.Code,
    ///     Message = details.Message,
    ///     TraceId = ctx.TraceIdentifier,
    /// });
    /// </code>
    /// </summary>
    public void Set<TEnvelope>(Func<FailureDetails, HttpContext, TEnvelope> factory)
    {
        Factory = (details, ctx) => factory(details, ctx)!;
    }
}
