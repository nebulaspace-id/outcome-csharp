using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Middleware;

/// <summary>
/// Catches unhandled exceptions and produces the SAME error envelope the library uses
/// for expected failures (via <see cref="IHttpResultFactory.FromFailureDetails"/>), so
/// every error — expected or not — shares one shape.
///
/// <para>
/// Mapped exceptions are logged as warnings (they are expected control flow); unhandled
/// exceptions are logged as errors. Only the response body's detail is controlled by
/// <see cref="ErrorOptions.IncludeExceptionDetails"/> — mapped exceptions keep the
/// mapper's message by design.
/// </para>
///
/// <para>
/// If the response has already started streaming, the exception is re‑thrown rather
/// than corrupting the output. CORS and <c>Vary</c> headers survive the response reset.
/// </para>
/// </summary>
internal sealed class OutcomeExceptionMiddleware : IMiddleware
{
    private const string GenericErrorMessage = "An error occurred while processing your request.";
    private const string GenericBadRequestMessage = "The request could not be processed.";

    private readonly IHttpResultFactory _resultFactory;
    private readonly ILogger<OutcomeExceptionMiddleware> _logger;
    private readonly OutcomeOptions _options;

    public OutcomeExceptionMiddleware(
        IHttpResultFactory resultFactory,
        ILogger<OutcomeExceptionMiddleware> logger,
        OutcomeOptions options)
    {
        _resultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected. Writing a 500 is pointless and would throw again;
            // let the server treat the request as aborted.
            throw;
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Unhandled exception after the response started: {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                throw;
            }

            // 1. An explicit mapping via options.Error.MapException<TException> wins — the
            //    mapper's HTTP intent is authoritative.
            if (_options.TryGetExceptionMapper(ex.GetType(), out var mapper))
            {
                _logger.LogWarning(ex, "Mapped exception: {Method} {Path}",
                    context.Request.Method, context.Request.Path);

                ResetResponse(context.Response);
                await _resultFactory.FromFailureDetails(mapper(ex)).ExecuteAsync(context);
                return;
            }

            // 2. Malformed requests (bad JSON, body too large, ...) already carry the right
            //    status code; do not downgrade them to 500.
            if (ex is BadHttpRequestException badRequest)
            {
                _logger.LogWarning(ex, "Bad request: {Method} {Path}",
                    context.Request.Method, context.Request.Path);

                var message = _options.Error.IncludeExceptionDetails ? ex.Message : GenericBadRequestMessage;
                ResetResponse(context.Response);
                await _resultFactory
                    .FromFailureDetails(new FailureDetails(badRequest.StatusCode, message))
                    .ExecuteAsync(context);
                return;
            }

            // 3. Unhandled exception — generic 500 envelope. The exception message is
            //    included only when IncludeExceptionDetails is on; otherwise a generic
            //    message is returned (the full exception is still logged server-side).
            _logger.LogError(ex, "Unhandled exception: {Method} {Path}",
                context.Request.Method, context.Request.Path);

            var detail = _options.Error.IncludeExceptionDetails ? ex.Message : GenericErrorMessage;
            ResetResponse(context.Response);
            await _resultFactory
                .FromFailureDetails(new FailureDetails(StatusCodes.Status500InternalServerError, detail))
                .ExecuteAsync(context);
        }
    }

    /// <summary>
    /// Clears the partially written response without dropping CORS or <c>Vary</c> headers
    /// set by upstream middleware — otherwise browsers cannot read the error body.
    /// </summary>
    private static void ResetResponse(HttpResponse response)
    {
        var preserved = new List<KeyValuePair<string, StringValues>>();
        foreach (var header in response.Headers)
        {
            if (header.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header.Key, "Vary", StringComparison.OrdinalIgnoreCase))
            {
                preserved.Add(header);
            }
        }

        response.Clear();

        foreach (var header in preserved)
            response.Headers[header.Key] = header.Value;
    }
}
