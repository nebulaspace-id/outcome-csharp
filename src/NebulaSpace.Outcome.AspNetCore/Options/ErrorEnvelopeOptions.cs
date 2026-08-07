using System.Reflection;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Configuration for the error envelope: the TYPE and how it is built from a failure's
/// HTTP intent plus the current request. The envelope must implement
/// <see cref="IErrorEnvelope"/> so the library can build it. Because the serialized body
/// IS that type, OpenAPI (<c>.Produces&lt;T&gt;()</c>) documents the exact error shape,
/// and per-request fields (<c>traceId</c>, <c>path</c>, custom) are real C# properties,
/// not runtime dictionary keys.
///
/// By default no envelope is configured and the library falls back to the clean RFC 7807
/// <see cref="DefaultProblemDetails"/> (title/status/detail only).
/// </summary>
public sealed class ErrorEnvelopeOptions
{
    /// <summary>
    /// Builds a concrete envelope instance from the failure's HTTP intent and the current
    /// request. <c>null</c> until <see cref="Set(Type)"/> is called, after which
    /// <see cref="ErrorEnvelopeResult"/> uses it instead of <see cref="DefaultProblemDetails"/>.
    /// </summary>
    internal Func<FailureDetails, HttpContext, object>? Factory { get; private set; }

    /// <summary>
    /// Sets the error envelope to a type implementing <see cref="IErrorEnvelope"/>:
    /// <code>
    /// options.ErrorEnvelope.Set(typeof(ErrorResponse));
    /// </code>
    /// The library invokes the static <c>From(FailureDetails, HttpContext)</c> per failure.
    /// A type that does not implement <see cref="IErrorEnvelope"/> throws
    /// <see cref="InvalidOperationException"/> here, at startup — not at request time.
    /// </summary>
    public void Set(Type envelopeType)
    {
        if (!typeof(IErrorEnvelope).IsAssignableFrom(envelopeType))
        {
            throw new InvalidOperationException(
                $"ErrorEnvelope.Set requires the envelope to implement IErrorEnvelope, " +
                $"whose 'From(FailureDetails details, HttpContext http)' builds the response " +
                $"body. '{envelopeType.FullName}' does not implement IErrorEnvelope.");
        }

        Factory = (details, ctx) =>
        {
            var from = envelopeType.GetMethod("From", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    $"ErrorEnvelope.Set: '{envelopeType.FullName}' implements IErrorEnvelope " +
                    $"but its 'From' method is missing or inaccessible.");
            return from.Invoke(null, new object[] { details, ctx })!;
        };
    }
}
