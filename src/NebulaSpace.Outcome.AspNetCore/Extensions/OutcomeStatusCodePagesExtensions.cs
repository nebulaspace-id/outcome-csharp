using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Extensions;

/// <summary>
/// Controls which framework-generated error responses
/// <see cref="OutcomeStatusCodePagesExtensions.UseOutcomeStatusCodePages"/> rewrites into
/// the configured error envelope.
/// </summary>
public sealed class OutcomeStatusCodePagesOptions
{
    /// <summary>
    /// Path prefixes that keep their original, body-less response. Defaults to the common
    /// non-API endpoints (<c>/swagger</c>, <c>/health</c>, <c>/_framework</c>,
    /// <c>/_blazor</c>, <c>/favicon.ico</c>) so their tooling keeps working.
    /// </summary>
    public IList<string> ExcludedPathPrefixes { get; } = new List<string>
    {
        "/swagger",
        "/health",
        "/_framework",
        "/_blazor",
        "/favicon.ico",
    };

    /// <summary>
    /// Optional additional predicate. Return <c>false</c> to keep the original response for
    /// a request; the excluded path prefixes are always skipped first.
    /// </summary>
    public Func<HttpContext, bool>? ShouldHandle { get; set; }
}

/// <summary>
/// Rewrites framework-generated error responses so they use the same error envelope as
/// failures created in application code.
///
/// <para>
/// Routing 404, method-matching 405, unsupported-media-type 415, authentication 401/403 and
/// rate-limiter 429 are written by the framework without running your handlers, so they
/// normally have an empty body. This middleware intercepts them and writes the configured
/// <see cref="IErrorEnvelope"/> using the defaults registered through
/// <see cref="ErrorOptions.MapStatusCode"/>.
/// </para>
///
/// <para>
/// Register it early — right after <c>UseOutcomeExceptionHandler()</c> and before
/// authentication, authorization, routing and endpoints — so it can observe status codes
/// set by any downstream middleware. <c>204</c>, <c>304</c> and <c>HEAD</c> responses are
/// left untouched.
/// </para>
/// </summary>
/// <example>
/// <code>
/// app.UseOutcomeExceptionHandler();
/// app.UseOutcomeStatusCodePages();
/// app.UseRateLimiter();
/// app.UseAuthentication();
/// app.UseAuthorization();
/// app.MapControllers();
/// </code>
/// </example>
public static class OutcomeStatusCodePagesExtensions
{
    /// <summary>
    /// Adds middleware that rewrites body-less error status codes (400–599) into the
    /// configured error envelope.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">Optional configuration for exclusions and filtering.</param>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="OutcomeHttpServiceCollectionExtensions.AddOutcome"/> was not called.
    /// </exception>
    public static IApplicationBuilder UseOutcomeStatusCodePages(
        this IApplicationBuilder app,
        Action<OutcomeStatusCodePagesOptions>? configure = null)
    {
        if (app is null) throw new ArgumentNullException(nameof(app));

        if (app.ApplicationServices.GetService(typeof(OutcomeOptions)) is not OutcomeOptions outcomeOptions)
        {
            throw new InvalidOperationException(
                $"{nameof(UseOutcomeStatusCodePages)} requires " +
                $"{nameof(OutcomeHttpServiceCollectionExtensions.AddOutcome)} to be called on the service collection first.");
        }

        var pagesOptions = new OutcomeStatusCodePagesOptions();
        configure?.Invoke(pagesOptions);

        return app.UseStatusCodePages(async context =>
        {
            var http = context.HttpContext;

            if (http.Response.HasStarted) return;
            if (HttpMethods.IsHead(http.Request.Method)) return;
            if (IsExcluded(http.Request.Path, pagesOptions.ExcludedPathPrefixes)) return;
            if (pagesOptions.ShouldHandle is { } shouldHandle && !shouldHandle(http)) return;

            var factory = http.RequestServices.GetRequiredService<IHttpResultFactory>();
            var description = outcomeOptions.GetStatusCodeDescription(http.Response.StatusCode);
            var details = new FailureDetails(
                http.Response.StatusCode, description.Message, description.Code);

            await factory.FromFailureDetails(details).ExecuteAsync(http);
        });
    }

    private static bool IsExcluded(PathString path, IList<string> prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (path.StartsWithSegments(new PathString(prefix), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
