using System.Collections.Concurrent;
using System.Reflection;
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
/// When an action returns an <see cref="Outcome{T}"/> (either directly or wrapped in
/// <see cref="ObjectResult"/>, <see cref="Task{TResult}"/>, etc.), the filter resolves
/// <see cref="IHttpResultFactory"/> from DI and maps success → <c>200 OK</c>,
/// failure → the mapped error response. Non-<see cref="Outcome{T}"/> values pass through
/// untouched.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class OutcomeResultAttribute : Attribute, IAsyncResultFilter
{
    private static readonly ConcurrentDictionary<Type, OutcomeProcessor> ProcessorCache = new();

    /// <inheritdoc />
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult objectResult)
        {
            var valueType = objectResult.Value?.GetType();

            if (valueType is not null && OutcomeTypeHelper.TryCreate(valueType, out var payloadType))
            {
                var factory = context.HttpContext.RequestServices.GetRequiredService<IHttpResultFactory>();
                var processor = ProcessorCache.GetOrAdd(payloadType, OutcomeProcessor.Create);

                var value = objectResult.Value!;
                var inner = processor.Execute(value, factory);
                context.Result = new OutcomeActionResult(inner);
            }
        }

        await next();
    }

    /// <summary>
    /// Identifies whether a runtime type is <see cref="Outcome{T}"/> and extracts <c>T</c>.
    /// </summary>
    private static class OutcomeTypeHelper
    {
        public static bool TryCreate(Type runtimeType, out Type payloadType)
        {
            var type = runtimeType;

            while (type is not null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Outcome<>))
                {
                    payloadType = type.GetGenericArguments()[0];
                    return true;
                }

                type = type.BaseType;
            }

            payloadType = null!;
            return false;
        }
    }

    /// <summary>
    /// Cached delegate pair for accessing <see cref="Outcome{T}"/> properties via reflection.
    /// One per <c>T</c> type, shared across all requests.
    /// </summary>
    private sealed class OutcomeProcessor
    {
        private static readonly MethodInfo FactoryOkMethod =
            typeof(IHttpResultFactory).GetMethods()
                .First(m => m.Name == nameof(IHttpResultFactory.Ok)
                            && m.GetParameters().Length == 1);

        private static readonly MethodInfo FactoryFromFailureMethod =
            typeof(IHttpResultFactory).GetMethod(nameof(IHttpResultFactory.FromFailure))!;

        private readonly Func<object, bool> _isFailure;
        private readonly Func<object, object> _getValue;
        private readonly Func<object, Failure> _getFailure;
        private readonly MethodInfo _okMethod;

        private OutcomeProcessor(
            Func<object, bool> isFailure,
            Func<object, object> getValue,
            Func<object, Failure> getFailure,
            MethodInfo okMethod)
        {
            _isFailure = isFailure;
            _getValue = getValue;
            _getFailure = getFailure;
            _okMethod = okMethod;
        }

        public IResult Execute(object outcome, IHttpResultFactory factory)
        {
            if (_isFailure(outcome))
                return factory.FromFailure(_getFailure(outcome));

            return (IResult)_okMethod.Invoke(factory, new[] { _getValue(outcome) })!;
        }

        public static OutcomeProcessor Create(Type payloadType)
        {
            var outcomeType = typeof(Outcome<>).MakeGenericType(payloadType);

            var isFailureProp = outcomeType.GetProperty(nameof(Outcome<object>.IsFailure))!;
            var valueProp = outcomeType.GetProperty(nameof(Outcome<object>.Value))!;
            var failureProp = outcomeType.GetProperty(nameof(Outcome<object>.Failure))!;

            // Box the struct and access properties through the boxed reference.
            Func<object, bool> isFailure = boxed => (bool)isFailureProp.GetValue(boxed)!;
            Func<object, object> getValue = boxed => valueProp.GetValue(boxed)!;
            Func<object, Failure> getFailure = boxed => (Failure)failureProp.GetValue(boxed)!;

            var okMethod = FactoryOkMethod.MakeGenericMethod(payloadType);

            return new OutcomeProcessor(isFailure, getValue, getFailure, okMethod);
        }
    }
}
