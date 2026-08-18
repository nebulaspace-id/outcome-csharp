using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// A 201 Created result that derives its <c>Location</c> header from the current
/// request path plus the new entity's id, so callers never write the URI by hand.
/// The body is wrapped in the configured success envelope.
///
/// <para>
/// The path is read from <see cref="HttpContext.Request.Path"/> at execution time,
/// so it can never drift out of sync with the route that served it — regardless of
/// whether the request came through <c>/orders</c> or <c>/admin/orders</c>.
/// </para>
/// </summary>
public sealed class CreatedResult<T> : IResult
{
    private readonly T _data;
    private readonly Func<T, object>? _idSelector;
    private readonly string? _locationUri;
    private readonly OutcomeOptions _options;

    /// <summary>
    /// Creates a 201 result with a <c>Location</c> derived from the request path + id.
    /// </summary>
    /// <param name="data">The new entity.</param>
    /// <param name="idSelector">Extracts the id from the entity (e.g. <c>o => o.Id</c>).</param>
    /// <param name="options">The configured <see cref="OutcomeOptions"/>.</param>
    public CreatedResult(T data, Func<T, object> idSelector, OutcomeOptions options)
    {
        _data = data;
        _idSelector = idSelector;
        _options = options;
    }

    /// <summary>
    /// Creates a 201 result with a manually specified <c>Location</c>.
    /// </summary>
    /// <param name="data">The new entity.</param>
    /// <param name="locationUri">The full <c>Location</c> header value.</param>
    /// <param name="options">The configured <see cref="OutcomeOptions"/>.</param>
    public CreatedResult(T data, string locationUri, OutcomeOptions options)
    {
        _data = data;
        _locationUri = locationUri;
        _options = options;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.Headers.Location = _locationUri
            ?? $"{httpContext.Request.Path.ToString().TrimEnd('/')}/{_idSelector!(_data)}";

        // SuccessEnvelopeResult sets the status code, so we don't duplicate it here.
        var envelope = new SuccessEnvelopeResult<T>(
            _data, pagination: null, StatusCodes.Status201Created, _options);

        await envelope.ExecuteAsync(httpContext);
    }
}
