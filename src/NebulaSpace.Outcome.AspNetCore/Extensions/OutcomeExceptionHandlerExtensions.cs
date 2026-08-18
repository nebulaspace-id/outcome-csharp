using Microsoft.AspNetCore.Builder;
using NebulaSpace.Outcome.AspNetCore.Middleware;

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
    /// Adds the middleware to the pipeline. Should be registered <b>EARLY</b> (before
    /// routing) so it catches exceptions from downstream middleware and endpoints alike.
    ///
    /// <para>
    /// Requires <see cref="OutcomeHttpServiceCollectionExtensions.AddOutcome"/> in
    /// <c>ConfigureServices</c> — that call registers <c>OutcomeOptions</c>,
    /// <c>IHttpResultFactory</c>, and the middleware itself. No separate registration
    /// is needed for exception handling.
    /// </para>
    ///
    /// <para>
    /// Middleware runs outer‑to‑inner: a middleware listed first wraps everything after
    /// it. To log exceptions to your own store, register your logging middleware
    /// <b>before</b> this one — it catches, records, then re‑throws so the handler
    /// still produces the envelope.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var app = builder.Build();
    /// app.UseOutcomeExceptionHandler(); // BEFORE routing
    /// app.UseRouting();
    /// app.MapEndpoints();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseOutcomeExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<OutcomeExceptionMiddleware>();
}
