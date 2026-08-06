using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Options for the Result pattern ASP.NET Core integration,
/// configured at startup via <c>AddOutcomeHttp</c>.
/// </summary>
public sealed class ResultPatternOptions
{
    /// <summary>
    /// Configuration for the success envelope — whether successful responses are wrapped
    /// in <c>SuccessResponse&lt;T&gt;</c>. Disable it to return raw payloads:
    /// <c>options.SuccessEnvelope.Enabled = false</c>. Error responses always use an
    /// envelope regardless of this setting.
    /// </summary>
    public SuccessEnvelopeOptions SuccessEnvelope { get; } = new();

    /// <summary>
    /// Configuration for the error envelope — properties injected into every error
    /// response. The default is clean RFC 7807 <see cref="Models.OutcomeProblemDetails"/>;
    /// use <see cref="ErrorEnvelopeOptions.Add"/> to add <c>traceId</c>,
    /// <c>path</c>, or any custom property. Properties only appear on error responses,
    /// never on success responses.
    /// </summary>
    public ErrorEnvelopeOptions ErrorEnvelope { get; } = new();

    /// <summary>
    /// Configuration for mapping application-defined failure types to HTTP responses.
    /// Use <see cref="FailureMappingOptions.Map{T}"/> to register a mapper:
    /// <c>options.Failures.Map&lt;MyFailure&gt;(f =&gt; ErrorResponse.NotFound(...))</c>.
    /// </summary>
    public FailureMappingOptions Failures { get; } = new();

    /// <summary>Registered failure mappers, keyed by failure type.</summary>
    internal Dictionary<Type, Func<Failure, IErrorResponse>> CustomFailureMappers =>
        Failures.Mappers;

    /// <summary>
    /// Properties injected into every error envelope response, keyed by JSON property name.
    /// Re-adding a name (e.g. <c>"traceId"</c>) overwrites the previous value factory.
    /// </summary>
    internal Dictionary<string, Func<HttpContext, object?>> ErrorEnvelopeProperties =>
        ErrorEnvelope.Properties;

    /// <summary>
    /// Finds a registered mapper for <paramref name="type"/>, walking its base-type chain
    /// so a subclass of a registered failure type inherits that failure's mapping.
    /// Returns the mapper registered for the nearest ancestor (most derived).
    /// </summary>
    internal bool TryGetFailureMapper(Type type, out Func<Failure, IErrorResponse> mapper)
    {
        for (var current = type; current is not null && current != typeof(Failure); current = current.BaseType!)
        {
            if (CustomFailureMappers.TryGetValue(current, out mapper!))
            {
                return true;
            }
        }

        mapper = null!;
        return false;
    }
}
