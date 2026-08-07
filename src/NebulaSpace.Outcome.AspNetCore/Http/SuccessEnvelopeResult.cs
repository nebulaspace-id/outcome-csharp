using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// A successful <see cref="IResult"/> that wraps the payload in the configured success
/// envelope — or returns it raw when <see cref="SuccessEnvelopeOptions.Enabled"/> is
/// <c>false</c>. The envelope is built at execution time so the factory can read the
/// current <see cref="HttpContext"/> (for <c>traceId</c>, <c>path</c>, ...) and the
/// response's status code.
/// </summary>
public sealed class SuccessEnvelopeResult<T> : IResult
{
    private readonly T? _data;
    private readonly PageInfo? _pagination;
    private readonly int _statusCode;
    private readonly ResultPatternOptions _options;

    public SuccessEnvelopeResult(T? data, PageInfo? pagination, int statusCode, ResultPatternOptions options)
    {
        _data = data;
        _pagination = pagination;
        _statusCode = statusCode;
        _options = options;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var success = _options.SuccessEnvelope;

        object body = !success.Enabled
            ? _data!
            : success.Factory is { } factory
                ? factory(_data, _pagination, httpContext, _statusCode)
                : BuildDefault(_data, _pagination, httpContext);

        httpContext.Response.StatusCode = _statusCode;
        // Cast to object so System.Text.Json serializes the runtime type's full shape.
        await httpContext.Response.WriteAsJsonAsync(body);
    }

    /// <summary>Builds the default envelope via its <c>From</c> factory, matching its status code to the response.</summary>
    private DefaultSuccessResponse<T> BuildDefault(T? data, PageInfo? pagination, HttpContext httpContext) =>
        (DefaultSuccessResponse<T>)DefaultSuccessResponse<T>.From(data!, pagination, httpContext, _statusCode);
}
