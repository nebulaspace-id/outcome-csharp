using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// An <see cref="IResult"/> that writes an error envelope. When a typed envelope is
/// configured (<c>options.Error.SetResponse(typeof(MyError))</c>), the body is the
/// user‑defined type, built from the failure's HTTP intent (<see cref="FailureDetails"/>)
/// and the current <see cref="HttpContext"/>, and serialized as‑is — so OpenAPI documents
/// the exact shape. Otherwise the default RFC 7807 <see cref="DefaultErrorResponse"/>
/// is written. The HTTP status always comes from <see cref="FailureDetails.StatusCode"/>.
/// </summary>
public sealed class ErrorEnvelopeResult : IResult
{
    private readonly FailureDetails _details;
    private readonly OutcomeOptions _options;

    public ErrorEnvelopeResult(FailureDetails details, OutcomeOptions options)
    {
        _details = details;
        _options = options;
    }

    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext) =>
        ExecuteAsync(httpContext, httpContext.RequestAborted);

    /// <summary>
    /// Writes the error envelope, honoring <paramref name="cancellationToken"/> so a
    /// client that disconnects mid‑write doesn't waste the serialization work. Falls back
    /// to <see cref="HttpContext.RequestAborted"/> when called through the interface.
    /// </summary>
    public async Task ExecuteAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        object envelope = _options.ErrorFactory is { } factory
            ? factory(_details, httpContext)
            : BuildDefaultErrorResponse(_details);

        httpContext.Response.StatusCode = _details.StatusCode;
        // Cast to object so System.Text.Json serializes the runtime type's full shape.
        await httpContext.Response.WriteAsJsonAsync(envelope, cancellationToken);
    }

    /// <summary>Builds the default clean RFC 7807 problem response from a failure's intent.</summary>
    private static DefaultErrorResponse BuildDefaultErrorResponse(FailureDetails details) => new()
    {
        Title = ReasonPhrases.GetReasonPhrase(details.StatusCode),
        Status = details.StatusCode,
        Detail = details.Message ?? string.Empty,
    };
}
