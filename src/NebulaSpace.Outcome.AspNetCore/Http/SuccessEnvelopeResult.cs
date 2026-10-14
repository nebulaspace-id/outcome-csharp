using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// A successful <see cref="IResult"/> for responses with no payload.
/// When a custom no-payload envelope is registered via
/// <c>options.Success.SetResponse(...)</c>, it delegates to that factory with
/// <c>null</c> data. Otherwise it uses <see cref="DefaultSuccessResponse"/> — a
/// status-code-only envelope.
/// </summary>
public sealed class SuccessEnvelopeResult : IResult
{
    private readonly int _statusCode;
    private readonly OutcomeOptions _options;

    public SuccessEnvelopeResult(int statusCode, OutcomeOptions options)
    {
        _statusCode = statusCode;
        _options = options;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        object body = !_options.Success.Enabled
            ? new {}
            : _options.Success.EmptyFactory is { } emptyFactory
                ? emptyFactory(httpContext, _statusCode)
                : new DefaultSuccessResponse { StatusCode = _statusCode };

        httpContext.Response.StatusCode = _statusCode;
        await httpContext.Response.WriteAsJsonAsync(body);
    }
}

/// <summary>
/// A successful <see cref="IResult"/> that wraps the payload in the configured success
/// envelope — or returns it raw when <see cref="SuccessOptions.Enabled"/> is
/// <c>false</c>. The envelope is built at execution time so the factory can read the
/// current <see cref="HttpContext"/> (for <c>traceId</c>, <c>path</c>, etc.) and the
/// response's status code.
///
/// <para>
/// The app's metadata is carried through as an <see cref="object"/> and never inspected
/// by the library. One envelope shape handles both meta and non-meta responses; when no
/// envelope is registered, <see cref="DefaultSuccessResponse{T}"/> is used.
/// </para>
/// </summary>
public sealed class SuccessEnvelopeResult<T> : IResult
{
    private readonly T? _data;
    private readonly object? _meta;
    private readonly int _statusCode;
    private readonly OutcomeOptions _options;

    public SuccessEnvelopeResult(T? data, object? meta, int statusCode, OutcomeOptions options)
    {
        _data = data;
        _meta = meta;
        _statusCode = statusCode;
        _options = options;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        // When the envelope is disabled, return the raw payload. If the payload is null,
        // return an empty object so WriteAsJsonAsync produces "{}" instead of "null" —
        // a null body that says the request was successful is misleading.
        object body = !_options.Success.Enabled
            ? (object?)_data ?? new {}
            : BuildEnvelope(httpContext);

        httpContext.Response.StatusCode = _statusCode;
        // Cast to object so System.Text.Json serializes the runtime type's full shape.
        await httpContext.Response.WriteAsJsonAsync(body);
    }

    private object BuildEnvelope(HttpContext httpContext) =>
        _options.SuccessFactory is { } factory
            ? factory(_data, _meta, httpContext, _statusCode)
            : (object)DefaultSuccessResponse<T>.From(_data!, _meta, httpContext, _statusCode);
}
