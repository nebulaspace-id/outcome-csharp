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
/// When an action returns an <see cref="Outcome{T}"/> (directly or wrapped in
/// <see cref="ObjectResult"/>), the filter resolves <see cref="IHttpResultFactory"/> from DI
/// and maps success → <c>200 OK</c>, failure → the mapped error response. Dispatch goes
/// through the <see cref="IOutcome"/> interface — no reflection, so it is NativeAOT-safe.
/// Non-<see cref="Outcome{T}"/> values pass through untouched.
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

            var inner = outcome.IsSuccess
                ? factory.Ok(outcome.Value)
                : factory.FromFailure(outcome.Failure);

            context.Result = new OutcomeActionResult(inner);
        }

        await next();
    }
}
