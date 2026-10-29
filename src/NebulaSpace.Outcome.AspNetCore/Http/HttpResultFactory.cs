using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// Default <see cref="IHttpResultFactory"/> that builds ASP.NET Core <see cref="IResult"/>
/// responses from payloads and failures, honoring the configured
/// <see cref="OutcomeOptions"/> supplied through constructor injection.
/// </summary>
internal sealed class HttpResultFactory : IHttpResultFactory
{
    private const string GenericErrorMessage = "An error occurred while processing your request.";

    private readonly OutcomeOptions _options;

    /// <summary>
    /// Creates a factory bound to <paramref name="options"/>. One instance per host;
    /// each host can configure the library independently.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    public HttpResultFactory(OutcomeOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    // A no-payload success closes the envelope as object when no custom envelope is
    // registered, so Data stays null and WhenWritingNull omits it.
    public IResult Ok() =>
        new SuccessEnvelopeResult<object>(null, meta: null, StatusCodes.Status200OK, _options);

    /// <inheritdoc />
    public IResult Ok<T>(T? data = default) =>
        new SuccessEnvelopeResult<T>(data, meta: null, StatusCodes.Status200OK, _options);

    /// <inheritdoc />
    public IResult Ok<T>(T data, object? meta) =>
        new SuccessEnvelopeResult<T>(data, meta, StatusCodes.Status200OK, _options);

    /// <inheritdoc />
    public IResult Created<T>(string locationUri, T data) =>
        new SuccessEnvelopeResult<T>(data, meta: null, StatusCodes.Status201Created, _options, locationUri);

    /// <inheritdoc />
    public IResult NoContent() =>
        Results.NoContent();

    /// <inheritdoc />
    public IResult BadRequest(string? message = null, string? code = null, IReadOnlyDictionary<string, string[]>? errors = null) =>
        Envelope(new FailureDetails(StatusCodes.Status400BadRequest, message, code, errors));

    /// <inheritdoc />
    public IResult Unauthorized(string? message = null, string? code = null, string? challenge = null) =>
        Envelope(new FailureDetails(StatusCodes.Status401Unauthorized, message, code), challenge);

    /// <inheritdoc />
    public IResult Forbidden(string? message = null, string? code = null) =>
        Envelope(new FailureDetails(StatusCodes.Status403Forbidden, message, code));

    /// <inheritdoc />
    public IResult NotFound(string? message = null, string? code = null) =>
        Envelope(new FailureDetails(StatusCodes.Status404NotFound, message, code));

    /// <inheritdoc />
    public IResult Conflict(string? message = null, string? code = null) =>
        Envelope(new FailureDetails(StatusCodes.Status409Conflict, message, code));

    /// <inheritdoc />
    public IResult FromFailure(Failure failure)
    {
        if (failure is null) throw new ArgumentNullException(nameof(failure));

        // 1. An explicit mapping via options.Error.MapFailure<TFailure> wins over a
        //    failure's own mapping, so callers can override built-in failures too.
        if (_options.TryGetFailureMapper(failure.GetType(), out var custom))
        {
            return Envelope(custom(failure));
        }

        // 2. Failures that know their HTTP intent (Open/Closed: new failure types
        //    add HTTP behavior without touching this class).
        if (failure is IHttpFailure httpFailure)
        {
            return Envelope(httpFailure.ToDetails());
        }

        // 3. Unknown failures degrade to a 500 instead of throwing. The message stays
        //    generic: an unmapped failure may wrap an internal exception, and the error
        //    envelope is client-visible. Failures that want a specific client message
        //    implement IHttpFailure or are registered with MapFailure.
        return Envelope(new FailureDetails(StatusCodes.Status500InternalServerError, GenericErrorMessage));
    }

    /// <summary>
    /// Builds the error envelope from a failure's HTTP intent, through the configured
    /// <see cref="ErrorOptions"/> (typed envelope) or the default RFC 7807
    /// <see cref="DefaultErrorResponse"/>.
    /// </summary>
    private IResult Envelope(FailureDetails details, string? wwwAuthenticate = null) =>
        new ErrorEnvelopeResult(details, _options, wwwAuthenticate);

    /// <inheritdoc />
    public IResult FromFailureDetails(FailureDetails details)
    {
        if (details is null) throw new ArgumentNullException(nameof(details));
        return Envelope(details);
    }
}
