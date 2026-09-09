using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// A 201 Created result with a manually specified <c>Location</c> header.
/// The body is wrapped in the configured success envelope.
/// </summary>
public sealed class CreatedResult<T> : IResult
{
    private readonly T _data;
    private readonly string _locationUri;
    private readonly OutcomeOptions _options;

    /// <summary>
    /// Creates a 201 result with a manually specified <c>Location</c>.
    /// </summary>
    /// <param name="locationUri">The full <c>Location</c> header value.</param>
    /// <param name="data">The new entity.</param>
    /// <param name="options">The configured <see cref="OutcomeOptions"/>.</param>
    public CreatedResult(string locationUri, T data, OutcomeOptions options)
    {
        _data = data;
        _locationUri = locationUri;
        _options = options;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.Headers.Location = _locationUri;

        // SuccessEnvelopeResult sets the status code, so we don't duplicate it here.
        var envelope = new SuccessEnvelopeResult<T>(
            _data, pagination: null, StatusCodes.Status201Created, _options);

        await envelope.ExecuteAsync(httpContext);
    }
}
