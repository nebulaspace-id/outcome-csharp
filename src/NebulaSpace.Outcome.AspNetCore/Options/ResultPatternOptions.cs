using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Options for the Result pattern ASP.NET Core integration,
/// configured at startup via <c>AddOutcomeHttp</c>.
/// </summary>
public sealed class ResultPatternOptions
{
    /// <summary>
    /// Configuration for the success envelope — whether successful responses are wrapped
    /// in <c>DefaultSuccessResponse&lt;T&gt;</c>. Disable it to return raw payloads:
    /// <c>options.SuccessEnvelope.Enabled = false</c>. Error responses always use an
    /// envelope regardless of this setting.
    /// </summary>
    public SuccessEnvelopeOptions SuccessEnvelope { get; } = new();

    /// <summary>
    /// Configuration for the error envelope — the TYPE of the error body and how it is
    /// built. The default is clean RFC 7807 <see cref="DefaultProblemDetails"/>
    /// (title/status/detail only). Use <see cref="ErrorEnvelopeOptions.Set{T}"/> to make
    /// the envelope any class you want — it is serialized as-is, so OpenAPI
    /// (<c>.Produces&lt;T&gt;()</c>) documents the exact error shape and per-request
    /// fields are real properties, not runtime dictionary keys.
    /// </summary>
    public ErrorEnvelopeOptions ErrorEnvelope { get; } = new();

    /// <summary>
    /// Configuration for mapping application-defined failure types to HTTP intent.
    /// Use <see cref="FailureMappingOptions.Map{T}"/> to register a mapper:
    /// <c>options.Failures.Map&lt;MyFailure&gt;(f =&gt; new FailureDetails(404, f.Message, f.Code))</c>.
    /// The configured error envelope renders the intent.
    /// </summary>
    public FailureMappingOptions Failures { get; } = new();

    /// <summary>Registered failure mappers, keyed by failure type.</summary>
    internal Dictionary<Type, Func<Failure, FailureDetails>> CustomFailureMappers =>
        Failures.Mappers;

    /// <summary>
    /// The configured success-envelope factory: builds the concrete envelope type from the
    /// payload, pagination, request, and status. <c>null</c> when the default
    /// <see cref="Models.DefaultSuccessResponse{T}"/> is used.
    /// </summary>
    internal Func<object?, PageInfo?, HttpContext, int, object>? SuccessEnvelopeFactory =>
        SuccessEnvelope.Factory;

    /// <summary>
    /// The configured error-envelope factory: builds the concrete envelope type from a
    /// failure's HTTP intent and the current request. <c>null</c> when the default RFC 7807
    /// <see cref="DefaultProblemDetails"/> is used.
    /// </summary>
    internal Func<FailureDetails, HttpContext, object>? ErrorEnvelopeFactory =>
        ErrorEnvelope.Factory;

    /// <summary>
    /// Finds a registered mapper for <paramref name="type"/>, walking its base-type chain
    /// so a subclass of a registered failure type inherits that failure's mapping.
    /// Returns the mapper registered for the nearest ancestor (most derived).
    /// </summary>
    internal bool TryGetFailureMapper(Type type, out Func<Failure, FailureDetails> mapper)
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
