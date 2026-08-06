using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Configuration for mapping exception types to concrete error envelopes, handled by
/// <see cref="Middleware.OutcomeExceptionMiddleware"/>. Registered mappers are consulted
/// BEFORE the generic 500 fallback, so an explicit mapping wins over the default
/// Internal Server Error response. The returned envelope is wrapped with the configured
/// <see cref="ErrorEnvelopeOptions"/> properties automatically, and — unlike the generic
/// fallback — is returned as-is regardless of
/// <see cref="Middleware.OutcomeExceptionHandlerOptions.IncludeExceptionDetails"/>.
/// </summary>
public sealed class ExceptionMappingOptions
{
    /// <summary>Registered mappers, keyed by the exception type they map.</summary>
    internal Dictionary<Type, Func<Exception, IErrorResponse>> Mappers { get; } = new();

    /// <summary>
    /// Registers a mapping from an exception type to a concrete error envelope
    /// (<see cref="IErrorResponse"/>). A subclass of a registered exception type
    /// inherits that exception's mapping (the nearest ancestor wins).
    /// </summary>
    public void Map<TException>(Func<TException, IErrorResponse> mapper)
        where TException : Exception
        => Mappers[typeof(TException)] = ex => mapper((TException)ex);
}
