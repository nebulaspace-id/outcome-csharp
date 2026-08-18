using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NebulaSpace.Outcome.AspNetCore.Failures;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Middleware;

/// <summary>
/// Catches unhandled exceptions and produces the SAME error envelope the library uses
/// for expected failures (via <see cref="IHttpResultFactory.FromFailure"/>), so every
/// error — expected or not — shares one shape.
///
/// <para>
/// The exception is logged in full via <see cref="ILogger"/>; only the response body's
/// detail is controlled by <see cref="ErrorOptions.IncludeExceptionDetails"/> (which
/// applies only to the generic 500 fallback — mapped exceptions bypass it).
/// </para>
///
/// <para>
/// If the response has already started streaming, the exception is re‑thrown rather
/// than corrupting the output.
/// </para>
/// </summary>
public sealed class OutcomeExceptionMiddleware : IMiddleware
{
    private readonly IHttpResultFactory _resultFactory;
    private readonly ILogger<OutcomeExceptionMiddleware> _logger;
    private readonly OutcomeOptions _options;

    public OutcomeExceptionMiddleware(
        IHttpResultFactory resultFactory,
        ILogger<OutcomeExceptionMiddleware> logger,
        OutcomeOptions options)
    {
        _resultFactory = resultFactory;
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Method} {Path}",
                context.Request.Method, context.Request.Path);

            // Response already started streaming — rewriting it would corrupt the output.
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();

            // 1. An explicit mapping via options.Error.MapException<TException> wins — it
            //    returns the failure's HTTP INTENT, which the configured error envelope
            //    renders (bypasses IncludeExceptionDetails).
            if (_options.TryGetExceptionMapper(ex.GetType(), out var mapper))
            {
                await _resultFactory.FromFailureDetails(mapper(ex)).ExecuteAsync(context);
                return;
            }

            // 2. Unhandled exception — generic 500 envelope. The exception message is
            //    included only when IncludeExceptionDetails is on; otherwise a generic
            //    message is returned (the full exception is still logged server-side).
            var message = _options.Error.IncludeExceptionDetails
                ? ex.Message
                : "An error occurred while processing your request.";
            await _resultFactory.FromFailure(new InternalServerErrorFailure(message))
                .ExecuteAsync(context);
        }
    }
}
