using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// A successful <see cref="IResult"/> that wraps the payload in the configured success
/// envelope — or returns it raw when <see cref="SuccessOptions.Enabled"/> is
/// <c>false</c>. The envelope is built at execution time so the factory can read the
/// current <see cref="HttpContext"/> (for <c>traceId</c>, <c>path</c>, etc.) and the
/// response's status code.
///
/// <para>
/// When no custom envelope is configured, falls back to
/// <see cref="DefaultSuccessResponse{T}"/>.
/// </para>
/// </summary>
public sealed class SuccessEnvelopeResult<T> : IResult
{
    private readonly T? _data;
    private readonly PageInfo? _pagination;
    private readonly int _statusCode;
    private readonly OutcomeOptions _options;

    public SuccessEnvelopeResult(T? data, PageInfo? pagination, int statusCode, OutcomeOptions options)
    {
        _data = data;
        _pagination = pagination;
        _statusCode = statusCode;
        _options = options;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var success = _options.Success;

        // When the envelope is disabled, return the raw payload. If the payload is null,
        // return an empty object so WriteAsJsonAsync produces "{}" instead of "null" —
        // a null body that says the request was successful is misleading.
        object body = !success.Enabled
            ? (object?)_data ?? new {}
            : success.Factory is { } factory
                ? factory(_data, _pagination, httpContext, _statusCode)
                : BuildDefault(_data, _pagination, httpContext);

        httpContext.Response.StatusCode = _statusCode;
        // Cast to object so System.Text.Json serializes the runtime type's full shape.
        await httpContext.Response.WriteAsJsonAsync(body);
    }

    /// <summary>
    /// Builds the default envelope via its <c>From</c> factory, matching its status code
    /// to the response.
    /// </summary>
    private DefaultSuccessResponse<T> BuildDefault(T? data, PageInfo? pagination, HttpContext httpContext) =>
        (DefaultSuccessResponse<T>)DefaultSuccessResponse<T>.From(data!, pagination, httpContext, _statusCode);
}
