using Microsoft.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Configuration for the error envelope: the per-request properties injected into every
/// error response the library produces (via <c>IErrorResponse.AddProperty</c> at execution
/// time). Properties registered here appear ONLY on error responses — never on success
/// responses. By default none are registered; the body is clean RFC 7807
/// <see cref="Models.OutcomeProblemDetails"/>.
/// </summary>
public sealed class ErrorEnvelopeOptions
{
    /// <summary>Registered property factories, keyed by JSON property name.</summary>
    internal Dictionary<string, Func<HttpContext, object?>> Properties { get; } = new();

    /// <summary>
    /// Registers a property that will be injected into every error envelope response.
    /// The <paramref name="valueFactory"/> runs per request, so values can depend on the
    /// current <see cref="HttpContext"/> (e.g. <c>ctx.TraceIdentifier</c>).
    /// </summary>
    public void Add(string propertyName, Func<HttpContext, object?> valueFactory)
        => Properties[propertyName] = valueFactory;
}
