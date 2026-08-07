using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Configuration for mapping application-defined failure types to HTTP intent
/// (<see cref="FailureDetails"/>). Registered mappers are consulted by
/// <see cref="Http.IHttpResultFactory.FromFailure"/> BEFORE a failure's own
/// <c>IHttpFailure</c> mapping, so an explicit mapping overrides the built-in ones.
/// The configured error envelope (<see cref="ErrorEnvelopeOptions.Set{T}"/>) renders the
/// intent, so every failure shares the app's one error shape.
/// </summary>
public sealed class FailureMappingOptions
{
    /// <summary>Registered mappers, keyed by the failure type they map.</summary>
    internal Dictionary<Type, Func<Failure, FailureDetails>> Mappers { get; } = new();

    /// <summary>
    /// Registers a mapping from a failure type to HTTP intent (<see cref="FailureDetails"/>).
    /// A subclass of a registered failure type inherits that failure's mapping (the nearest
    /// ancestor wins).
    /// </summary>
    public void Map<TFailure>(Func<TFailure, FailureDetails> mapper)
        where TFailure : Failure
        => Mappers[typeof(TFailure)] = failure => mapper((TFailure)failure);
}
