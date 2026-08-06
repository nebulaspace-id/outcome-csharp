using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// An <see cref="IResult"/> that writes an <see cref="IErrorResponse"/> with the
/// configured envelope properties (traceId, path, custom) injected at execution
/// time, when the <see cref="HttpContext"/> is available. Built exclusively by
/// <see cref="HttpResultFactory"/> — no response buffering or re-serialization.
/// </summary>
public sealed class ErrorEnvelopeResult : IResult
{
    private readonly IErrorResponse _envelope;
    private readonly ResultPatternOptions _options;

    public ErrorEnvelopeResult(IErrorResponse envelope, ResultPatternOptions options)
    {
        _envelope = envelope;
        _options = options;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        // Inject per-request properties (defaults: traceId, path) computed from HttpContext.
        foreach (var (propertyName, valueFactory) in _options.ErrorEnvelopeProperties)
        {
            var value = valueFactory(httpContext);
            if (value is null)
            {
                continue;
            }

            _envelope.AddProperty(propertyName, value);
        }

        httpContext.Response.StatusCode = _envelope.StatusCode;
        // Cast to object so System.Text.Json serializes the runtime type's full shape,
        // not just the IErrorResponse interface members.
        await httpContext.Response.WriteAsJsonAsync((object)_envelope);
    }
}
