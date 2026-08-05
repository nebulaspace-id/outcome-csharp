using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Options for the Result pattern ASP.NET Core integration,
/// configured at startup via <c>AddResultPattern</c>.
/// </summary>
public sealed class ResultPatternOptions
{
    /// <summary>Whether success responses are wrapped in <c>SuccessResponse&lt;T&gt;</c>. Default <c>true</c>.</summary>
    public bool EnvelopeSuccess { get; set; } = true;

    internal Dictionary<Type, Func<Failure, IResult>> CustomFailureMappers { get; } = new();

    /// <summary>
    /// Properties injected into every error envelope response, keyed by JSON property name.
    /// A <c>traceId</c> property (from <see cref="HttpContext.TraceIdentifier"/>) is registered
    /// by default; re-adding <c>"traceId"</c> overwrites it.
    /// </summary>
    internal Dictionary<string, Func<HttpContext, object?>> ErrorEnvelopeProperties { get; } = new();

    /// <summary>
    /// Registers a property that will be injected into every error envelope response.
    /// </summary>
    public void AddErrorEnvelopeProperty(string propertyName, Func<HttpContext, object?> valueFactory)
        => ErrorEnvelopeProperties[propertyName] = valueFactory;

    /// <summary>
    /// Registers a mapping for an application-defined failure type.
    /// Consulted by <see cref="HttpResult.FromFailure"/> before the built-in switches.
    /// </summary>
    public void MapFailure<TFailure>(Func<TFailure, IResult> mapper)
        where TFailure : Failure
        => CustomFailureMappers[typeof(TFailure)] = failure => mapper((TFailure)failure);
}
