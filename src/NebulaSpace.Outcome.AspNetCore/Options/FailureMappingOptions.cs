using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Configuration for mapping application-defined failure types to HTTP responses.
/// Registered mappers are consulted by <see cref="Http.IHttpResultFactory.FromFailure"/>
/// BEFORE a failure's own <c>IHttpFailure</c> mapping, so an explicit mapping overrides
/// the built-in ones. The returned envelope is wrapped with the configured
/// <see cref="ErrorEnvelopeOptions"/> properties automatically.
/// </summary>
public sealed class FailureMappingOptions
{
    /// <summary>Registered mappers, keyed by the failure type they map.</summary>
    internal Dictionary<Type, Func<Failure, IErrorResponse>> Mappers { get; } = new();

    /// <summary>
    /// Registers a mapping from a failure type to a concrete error envelope
    /// (<see cref="IErrorResponse"/>). A subclass of a registered failure type inherits
    /// that failure's mapping (the nearest ancestor wins).
    /// </summary>
    public void Map<TFailure>(Func<TFailure, IErrorResponse> mapper)
        where TFailure : Failure
        => Mappers[typeof(TFailure)] = failure => mapper((TFailure)failure);
}
