using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NebulaSpace.Outcome.AspNetCore.Mvc;

namespace NebulaSpace.Outcome.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering MVC-specific Outcome services.
/// </summary>
public static class OutcomeMvcServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OutcomeResultAttribute"/> as a global MVC action filter.
    /// All controller actions that return <see cref="Outcome{T}"/> will automatically
    /// have their result converted to a proper HTTP response with the configured envelope.
    ///
    /// <para>
    /// Requires <see cref="OutcomeHttpServiceCollectionExtensions.AddOutcome"/> to be
    /// called first.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddOutcome();
    /// builder.Services.AddControllers();
    /// builder.Services.AddOutcomeMvcFilter();
    /// </code>
    /// </example>
    public static IServiceCollection AddOutcomeMvcFilter(this IServiceCollection services)
    {
        services.Configure<MvcOptions>(options =>
            options.Filters.Add<OutcomeResultAttribute>());

        return services;
    }
}
