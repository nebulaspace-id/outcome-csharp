using Microsoft.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Contract for a typed error envelope. Implement it on the type the app wants serialized
/// for failed responses; the <c>From</c> factory builds the per-request body from a
/// failure's HTTP intent (<see cref="FailureDetails"/>) and the current request.
/// <para>
/// Register the type with <c>options.ErrorEnvelope.Set(typeof(MyError))</c> — the library
/// invokes <c>From</c> per failure. Because the serialized body IS that type,
/// <c>.Produces&lt;MyError&gt;()</c> documents the exact error shape, and per-request
/// fields (<c>traceId</c>, <c>path</c>, custom) are real C# properties, not runtime
/// dictionary keys.
/// </para>
/// </summary>
public interface IErrorEnvelope
{
    /// <summary>Builds the envelope instance for one failed response.</summary>
    /// <param name="details">The failure's HTTP intent (status, message, code, field errors).</param>
    /// <param name="httpContext">The current request.</param>
    static abstract IErrorEnvelope From(FailureDetails details, HttpContext httpContext);
}
