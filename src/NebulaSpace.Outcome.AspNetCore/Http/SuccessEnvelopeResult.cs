using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// A successful <see cref="IResult"/> that wraps the payload in the configured success
/// envelope — or returns it raw when <see cref="SuccessOptions.Enabled"/> is
/// <c>false</c>. The envelope is built at execution time so the factory can read the
/// current <see cref="HttpContext"/> (for <c>traceId</c>, <c>path</c>, etc.) and the
/// response's status code.
///
/// <para>
/// A non‑null <c>locationUri</c> also writes the <c>Location</c> header, turning the
/// result into a 201 Created with a body — the same code path as a 200 OK, so the
/// envelope is built exactly once.
/// </para>
/// </summary>
internal sealed class SuccessEnvelopeResult<T> : IResult
{
    private readonly T? _data;
    private readonly object? _meta;
    private readonly int _statusCode;
    private readonly string? _locationUri;
    private readonly OutcomeOptions _options;

    public SuccessEnvelopeResult(T? data, object? meta, int statusCode, OutcomeOptions options, string? locationUri = null)
    {
        if (locationUri is not null)
        {
            if (string.IsNullOrWhiteSpace(locationUri))
                throw new ArgumentException("The Location URI must not be empty.", nameof(locationUri));
            if (locationUri.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                throw new ArgumentException("The Location URI must not contain CR or LF characters.", nameof(locationUri));
        }

        _data = data;
        _meta = meta;
        _statusCode = statusCode;
        _locationUri = locationUri;
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        if (httpContext is null) throw new ArgumentNullException(nameof(httpContext));

        // When the envelope is disabled, return the raw payload. If the payload is null,
        // return an empty object so WriteAsJsonAsync produces "{}" instead of "null" —
        // a null body that says the request was successful is misleading.
        object body = !_options.Success.Enabled
            ? (object?)_data ?? new { }
            : BuildEnvelope(httpContext);

        httpContext.Response.StatusCode = _statusCode;
        if (_locationUri is not null)
            httpContext.Response.Headers.Location = _locationUri;

        // Cast to object so System.Text.Json serializes the runtime type's full shape.
        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken: httpContext.RequestAborted);
    }

    private object BuildEnvelope(HttpContext httpContext) =>
        _options.SuccessFactory is { } factory
            ? factory(_data, _meta, httpContext, _statusCode)
            : (object)DefaultSuccessResponse<T>.From(_data!, _meta, httpContext, _statusCode);
}
