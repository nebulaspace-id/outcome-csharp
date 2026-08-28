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
        if (services.Any(d => d.ServiceType == typeof(OutcomeOptions)))
            throw new InvalidOperationException(
                "AddOutcome has already been called. Configure additional options using the existing registration.");

        var options = new OutcomeOptions();
        configure?.Invoke(options);

        // By default, discover every IPaginatedQuery implementor across loaded assemblies
        // (reflection) and let each one copy the app's pagination caps into its static fields —
        // no wiring needed. For NativeAOT, set options.Pagination.ScanAssemblies = false and
        // register query types explicitly with options.Pagination.RegisterQuery<TQuery>().
        if (options.Pagination.ScanAssemblies)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;

                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).ToArray()!; }
                catch { continue; }

                foreach (var type in types.Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPaginatedQuery).IsAssignableFrom(t)))
                {
                    var configureMethod = type.GetMethod(
                        nameof(IPaginatedQuery.Configure),
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                        null, [typeof(PaginationOptions)], null);

                    configureMethod?.Invoke(null, [options.Pagination]);
                }
            }
        }

        // Explicit registrations (AOT-safe path) run regardless — Configure is dispatched
        // through the static abstract interface member, no reflection. Idempotent with the
        // scan above, so double registration is harmless.
        foreach (var configureQuery in options.Pagination.QueryConfigurators)
        {
            configureQuery(options.Pagination);
        }

        services.AddSingleton(options);
        services.AddSingleton<IHttpResultFactory, HttpResultFactory>();

        // Exception-handling middleware — configured from options.Error (same object).
        services.AddTransient<OutcomeExceptionMiddleware>();

        return services;
    }
}