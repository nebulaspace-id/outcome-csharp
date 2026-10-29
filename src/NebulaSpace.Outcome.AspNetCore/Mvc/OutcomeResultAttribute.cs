using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Mvc;

/// <summary>
/// An action filter that automatically converts <see cref="Outcome{T}"/> values returned
/// from MVC controller actions into proper HTTP responses. Apply per-action, per-controller,
/// or register globally via <c>AddOutcomeMvcFilter()</c>.
///
/// <para>
/// Mapping: failure → the configured error response; success with a payload → <c>200 OK</c>;
/// success with <see cref="Unit"/> → <c>204 No Content</c> (a payload-less outcome has no
/// body to serialize). Register the filter globally and return <c>Outcome&lt;T&gt;</c>
/// directly — no reflection, the boxed outcome dispatches through <see cref="IOutcome"/>.
/// Non-<see cref="Outcome{T}"/> values pass through untouched.
/// </para>
///
/// <para>
/// Need a 200 body for a <see cref="Unit"/> success, or custom metadata? Use
/// <c>outcome.ToActionResult(http, onSuccess)</c> instead of the filter.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class OutcomeResultAttribute : Attribute, IAsyncResultFilter
{
    /// <inheritdoc />
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: IOutcome outcome })
        {
            var factory = context.HttpContext.RequestServices.GetRequiredService<IHttpResultFactory>();

            IResult inner;
            if (!outcome.IsSuccess)
            {
                inner = factory.FromFailure(outcome.Failure!);
            }
            else if (outcome.Value is Unit)
            {
                inner = factory.NoContent();
            }
            else
            {
                inner = factory.Ok(outcome.Value);
            }

            context.Result = new OutcomeActionResult(inner);
        }

        await next();
    }
}
