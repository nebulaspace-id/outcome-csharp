using Microsoft.AspNetCore.Builder;
using NebulaSpace.Outcome.AspNetCore.Middleware;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Extensions;

/// <summary>
/// Wires the exception‑handling middleware so unhandled exceptions produce the same
/// error envelope as expected failures. Requires
/// <see cref="OutcomeHttpServiceCollectionExtensions.AddOutcome"/> in
/// <c>ConfigureServices</c> first — that single call registers the middleware, its
/// options, and <c>IHttpResultFactory</c>.
/// </summary>
public static class OutcomeExceptionHandlerExtensions
{
    /// <summary>
    /// Adds the middleware to the pipeline. Register it <b>FIRST</b> so it wraps rate
    /// limiting, routing, authentication, and endpoints alike.
    ///
    /// <para>
    /// Middleware runs outer‑to‑inner: a middleware listed first wraps everything after
    /// it. To log exceptions to your own store, register your logging middleware
    /// <b>before</b> this one — it catches, records, then re‑throws so the handler
    /// still produces the envelope.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="OutcomeHttpServiceCollectionExtensions.AddOutcome"/> was not called.
    /// </exception>
    /// <example>
    /// <code>
    /// var app = builder.Build();
    /// app.UseOutcomeExceptionHandler(); // FIRST — before rate limiting and routing
    /// app.UseRateLimiter();
    /// app.UseRouting();
    /// app.MapEndpoints();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseOutcomeExceptionHandler(this IApplicationBuilder app)
    {
        if (app.ApplicationServices.GetService(typeof(OutcomeOptions)) is null)
        {
            throw new InvalidOperationException(
                "UseOutcomeExceptionHandler requires AddOutcome(...) to be called when registering services. " +
                "Add builder.Services.AddOutcome() before building the application.");
        }

        return app.UseMiddleware<OutcomeExceptionMiddleware>();
    }
}
