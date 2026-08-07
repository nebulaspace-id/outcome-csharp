using Microsoft.Extensions.DependencyInjection;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Extensions;

public static class OutcomeHttpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Outcome ASP.NET Core HTTP integration. Options are supplied
    /// through the DI container and consumed via <see cref="IHttpResultFactory"/>,
    /// so multiple applications in one process can configure the library independently.
    /// The default error shape is clean RFC 7807 <see cref="DefaultProblemDetails"/>:
    /// only <c>title</c>/<c>status</c>/<c>detail</c>. Use
    /// <c>options.ErrorEnvelope.Set&lt;T&gt;</c> to make the error body any type you want —
    /// it is serialized as-is, so <c>.Produces&lt;T&gt;()</c> documents the exact shape.
    /// </summary>
    public static IServiceCollection AddOutcomeHttp(
        this IServiceCollection services,
        Action<ResultPatternOptions>? configure = null)
    {
        var options = new ResultPatternOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IHttpResultFactory, HttpResultFactory>();

        return services;
    }
}