using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Mvc;

namespace NebulaSpace.Outcome.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering MVC-specific Outcome services.
/// </summary>
public static class OutcomeMvcServiceCollectionExtensions
{
    private const string ValidationErrorCode = "validation_error";
    private const string ValidationErrorMessage = "One or more validation errors occurred.";

    /// <summary>
    /// Registers <see cref="OutcomeResultAttribute"/> as a global MVC action filter and maps
    /// automatic model-state failures (400) to the configured error envelope.
    /// All controller actions that return <see cref="Outcome{T}"/> will automatically
    /// have their result converted to a proper HTTP response with the configured envelope.
    ///
    /// <para>
    /// <c>[ApiController]</c> short-circuits invalid model state before the action runs, so
    /// without the mapping the response would be ASP.NET Core's
    /// <c>ValidationProblemDetails</c> instead of your own error envelope. The mapping is
    /// always on; failure details use the same field-keyed shape as
    /// <c>FailureDetails.Errors</c>.
    /// </para>
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

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var factory = context.HttpContext.RequestServices
                    .GetRequiredService<IHttpResultFactory>();

                return new OutcomeActionResult(factory.BadRequest(
                    ValidationErrorMessage,
                    ValidationErrorCode,
                    ModelStateErrorMapper.ToErrorDictionary(context.ModelState)));
            };
        });

        return services;
    }
}
