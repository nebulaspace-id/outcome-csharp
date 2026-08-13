using Microsoft.Extensions.DependencyInjection;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Middleware;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Pagination;

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
    /// The default error shape is clean RFC 7807 <see cref="DefaultErrorResponse"/>:
    /// only <c>title</c>/<c>status</c>/<c>detail</c>. Use
    /// <c>options.Error.SetResponse&lt;MyError&gt;()</c> — any type implementing
    /// <see cref="IErrorEnvelope"/> — to make the error body your own; it is serialized
    /// as-is, so <c>.Produces&lt;MyError&gt;()</c> documents the exact shape.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddOutcome(options =>
    /// {
    ///     options.Success.SetResponse&lt;SuccessResponse&gt;();
    ///     options.Error.SetResponse&lt;ErrorResponse&gt;();
    ///     options.Error.MapFailure&lt;QuotaExceededFailure&gt;(f =>
    ///         new FailureDetails(422, f.Message, f.Code));
    ///     options.Error.MapException&lt;DomainException&gt;(ex =>
    ///         new FailureDetails(422, ex.Message, ex.Code));
    ///     options.Error.IncludeExceptionDetails = env.IsDevelopment();
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOutcome(
        this IServiceCollection services,
        Action<OutcomeOptions>? configure = null)
    {
        var options = new OutcomeOptions();
        configure?.Invoke(options);

        // Copy PaginationOptions to the static defaults so [AsParameters]-bound query
        // types pick up the app's configured caps (they are parameterless for binding).
        OffsetQuery.MaxPage = Math.Max(1, options.Pagination.MaxPage);
        OffsetQuery.MaxLimit = Math.Max(1, options.Pagination.MaxLimit);
        CursorQuery.MaxLimit = Math.Max(1, options.Pagination.MaxLimit);

        services.AddSingleton(options);
        services.AddSingleton<IHttpResultFactory, HttpResultFactory>();

        // Exception-handling middleware — configured from options.Error (same object).
        services.AddTransient<OutcomeExceptionMiddleware>();

        return services;
    }
}