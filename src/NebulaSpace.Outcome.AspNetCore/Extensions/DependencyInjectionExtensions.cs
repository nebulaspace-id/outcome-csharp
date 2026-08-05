using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Middleware;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Extensions;

public static class ResultPatternServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Result pattern ASP.NET Core integration and applies the
    /// configured options to <see cref="HttpResult"/> and the trace id middleware.
    /// </summary>
    public static IServiceCollection AddResultPattern(
        this IServiceCollection services,
        Action<ResultPatternOptions>? configure = null)
    {
        var options = new ResultPatternOptions();
        configure?.Invoke(options);

        // Defaults: inject traceId and path into error envelopes unless overridden.
        if (!options.ErrorEnvelopeProperties.ContainsKey("traceId"))
        {
            options.AddErrorEnvelopeProperty("traceId", context => context.TraceIdentifier);
        }

        if (!options.ErrorEnvelopeProperties.ContainsKey("path"))
        {
            options.AddErrorEnvelopeProperty("path", context => context.Request.Path.ToString());
        }

        // Static holder so the static HttpResult helpers honor the options.
        ResultPattern.Options = options;

        services.AddSingleton(options);

        return services;
    }
}

public static class ResultPatternApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the error envelope middleware. Reads the registered options, falling back
    /// to the static default if <c>AddResultPattern</c> was not called.
    /// </summary>
    public static IApplicationBuilder UseResultPattern(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetService<ResultPatternOptions>() ?? ResultPattern.Options;
        return app.UseMiddleware<ErrorEnvelopeMiddleware>(options);
    }
}
