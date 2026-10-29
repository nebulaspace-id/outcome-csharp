using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Middleware;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Extensions;

public static class OutcomeHttpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Outcome ASP.NET Core HTTP integration in one call — success envelope,
    /// error envelope, failure/exception mappers, and exception-handling middleware.
    /// Options are supplied through the DI container and consumed via
    /// <see cref="IHttpResultFactory"/>, so multiple applications in one process can
    /// configure the library independently.
    ///
    /// <para>
    /// The options are frozen when this method returns. Mutating them afterwards throws
    /// <see cref="InvalidOperationException"/>; treat the callback as the only
    /// configuration surface.
    /// </para>
    ///
    /// <para>
    /// The default error shape is clean RFC 7807 <see cref="DefaultErrorResponse"/>
    /// written as <c>application/problem+json</c>: only <c>title</c>/<c>status</c>/<c>detail</c>
    /// (plus <c>errors</c> when the failure carries field errors). Use
    /// <c>options.Error.SetResponse&lt;MyError&gt;()</c> — any type implementing
    /// <see cref="IErrorEnvelope"/> — to make the error body your own; it is serialized
    /// as-is, so <c>.Produces&lt;MyError&gt;()</c> documents the exact shape.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException"><c>AddOutcome</c> was already called.</exception>
    /// <example>
    /// <code>
    /// builder.Services.AddOutcome(options =>
    /// {
    ///     options.Success.SetResponse(typeof(SuccessResponse&lt;&gt;));
    ///     options.Error.SetResponse&lt;ErrorResponse&gt;();
    ///     options.Error.MapFailure&lt;QuotaExceededFailure&gt;(f =>
    ///         new FailureDetails(422, f.Message, f.Code));
    ///     options.Error.MapException&lt;DomainException&gt;(ex =>
    ///         new FailureDetails(422, ex.Message, ex.Code));
    ///     options.Error.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOutcome(
        this IServiceCollection services,
        Action<OutcomeOptions>? configure = null)
    {
        if (services.Any(d => d.ServiceType == typeof(OutcomeOptions)))
            throw new InvalidOperationException(
                "AddOutcome has already been called. Configure additional options using the existing registration.");

        var options = new OutcomeOptions();
        configure?.Invoke(options);
        options.Freeze();

        services.TryAddSingleton(options);
        services.TryAddSingleton<IHttpResultFactory, HttpResultFactory>();

        // Exception-handling middleware — configured from options.Error (same object).
        // IMiddleware is resolved once by UseMiddleware, so a singleton registration
        // matches its actual lifetime.
        services.TryAddSingleton<OutcomeExceptionMiddleware>();

        return services;
    }
}
