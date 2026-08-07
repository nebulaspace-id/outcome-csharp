using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Configuration for mapping exception types to HTTP intent (<see cref="FailureDetails"/>),
/// handled by <see cref="Middleware.OutcomeExceptionMiddleware"/>. Registered mappers are
/// consulted BEFORE the generic 500 fallback, so an explicit mapping wins over the default
/// Internal Server Error response. The configured error envelope
/// (<see cref="ErrorEnvelopeOptions.Set{T}"/>) renders the intent, so every exception —
/// mapped or not — shares the app's one error shape.
/// </summary>
public sealed class ExceptionMappingOptions
{
    /// <summary>Registered mappers, keyed by the exception type they map.</summary>
    internal Dictionary<Type, Func<Exception, FailureDetails>> Mappers { get; } = new();

    /// <summary>
    /// Registers a mapping from an exception type to HTTP intent (<see cref="FailureDetails"/>).
    /// A subclass of a registered exception type inherits that exception's mapping (the
    /// nearest ancestor wins).
    /// </summary>
    public void Map<TException>(Func<TException, FailureDetails> mapper)
        where TException : Exception
        => Mappers[typeof(TException)] = ex => mapper((TException)ex);
}
