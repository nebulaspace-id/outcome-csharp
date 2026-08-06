using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NebulaSpace.Outcome.AspNetCore.Middleware;

namespace NebulaSpace.Outcome.AspNetCore.Extensions;

/// <summary>
/// Registers and wires the exception-handling middleware so unhandled exceptions produce
/// the same error envelope as expected failures.
/// </summary>
public static class OutcomeExceptionHandlerExtensions
{
    /// <summary>
    /// Registers the <see cref="OutcomeExceptionMiddleware"/> (and its options) in DI.
    /// Call once from <c>ConfigureServices</c>. Then call <c>UseOutcomeExceptionHandler</c>
    /// in the pipeline. Map known exception types via
    /// <c>options.Exceptions.Map&lt;TException&gt;</c> to a specific envelope; unhandled
    /// exceptions default to a generic 500 message. Enable
    /// <see cref="OutcomeExceptionHandlerOptions.IncludeExceptionDetails"/> (typically in
    /// Development) to surface the unhandled exception's message — mapped exceptions
    /// bypass this gating and use their mapper's envelope as-is.
    /// </summary>
    public static IServiceCollection AddOutcomeExceptionHandler(
        this IServiceCollection services,
        Action<OutcomeExceptionHandlerOptions>? configure = null)
    {
        // Idempotent: calling AddOutcomeExceptionHandler twice (or in addition to an
        // automatic registration) keeps the first-configured options.
        if (services.Any(d => d.ServiceType == typeof(OutcomeExceptionMiddleware)))
        {
            return services;
        }

        var options = new OutcomeExceptionHandlerOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddTransient<OutcomeExceptionMiddleware>();
        return services;
    }

    /// <summary>
    /// Adds the middleware to the pipeline. Should be registered EARLY (before routing)
    /// so it catches exceptions from downstream middleware and endpoints alike. Requires
    /// <see cref="AddOutcomeExceptionHandler"/> in <c>ConfigureServices</c> (which in turn
    /// needs <c>AddOutcomeHttp</c> for <c>IHttpResultFactory</c>).
    /// </summary>
    public static IApplicationBuilder UseOutcomeExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<OutcomeExceptionMiddleware>();
}
