using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// An <see cref="IResult"/> that writes an error envelope. When a typed envelope is
/// configured, the body is the user‑defined type; otherwise the default RFC 7807
/// <see cref="DefaultErrorResponse"/> is written with
/// <c>application/problem+json</c>. The HTTP status always comes from
/// <see cref="FailureDetails.StatusCode"/>.
/// </summary>
internal sealed class ErrorEnvelopeResult : IResult
{
    private const string ProblemJsonContentType = "application/problem+json";

    private readonly FailureDetails _details;
    private readonly OutcomeOptions _options;
    private readonly string? _wwwAuthenticate;

    public ErrorEnvelopeResult(FailureDetails details, OutcomeOptions options, string? wwwAuthenticate = null)
    {
        _details = details ?? throw new ArgumentNullException(nameof(details));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _wwwAuthenticate = wwwAuthenticate;
    }

    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext) =>
        ExecuteAsync(httpContext, httpContext.RequestAborted);

    /// <summary>
    /// Writes the error envelope, honoring <paramref name="cancellationToken"/> so a
    /// client that disconnects mid‑write doesn't waste the serialization work.
    /// </summary>
    public async Task ExecuteAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (httpContext is null) throw new ArgumentNullException(nameof(httpContext));

        var configuredFactory = _options.ErrorFactory;

        httpContext.Response.StatusCode = _details.StatusCode;

        if (_wwwAuthenticate is not null)
            httpContext.Response.Headers.WWWAuthenticate = _wwwAuthenticate;

        // Errors are request-specific and often unauthenticated; do not let a shared
        // cache replay them.
        if (_details.StatusCode is 401 or 403)
            httpContext.Response.Headers.CacheControl = "no-store";

        if (configuredFactory is not null)
        {
            var envelope = configuredFactory(_details, httpContext);
            // Cast to object so System.Text.Json serializes the runtime type's full shape.
            await httpContext.Response.WriteAsJsonAsync(
                (object)envelope,
                options: null,
                contentType: "application/json",
                cancellationToken);
            return;
        }

        await httpContext.Response.WriteAsJsonAsync(
            BuildDefaultErrorResponse(_details),
            options: null,
            contentType: ProblemJsonContentType,
            cancellationToken);
    }

    /// <summary>Builds the default clean RFC 7807 problem response from a failure's intent.</summary>
    private static DefaultErrorResponse BuildDefaultErrorResponse(FailureDetails details) => new()
    {
        Title = ReasonPhrases.GetReasonPhrase(details.StatusCode),
        Status = details.StatusCode,
        Detail = details.Message ?? string.Empty,
        Errors = details.Errors,
    };
}
