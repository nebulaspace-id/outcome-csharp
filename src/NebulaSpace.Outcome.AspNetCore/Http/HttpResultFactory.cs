using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// Default <see cref="IHttpResultFactory"/> that builds ASP.NET Core <see cref="IResult"/>
/// responses from payloads and failures, honoring the configured
/// <see cref="OutcomeOptions"/> supplied through constructor injection.
/// </summary>
public sealed class HttpResultFactory : IHttpResultFactory
{
    private readonly OutcomeOptions _options;

    /// <summary>
    /// Creates a factory bound to <paramref name="options"/>. One instance per host;
    /// each host can configure the library independently.
    /// </summary>
    public HttpResultFactory(OutcomeOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public IResult Ok() =>
        new SuccessEnvelopeResult(StatusCodes.Status200OK, _options);

    /// <inheritdoc />
    public IResult Ok<T>(T? data = default) =>
        SuccessEnvelopeResultFor(data, pagination: null, StatusCodes.Status200OK);

    /// <inheritdoc />
    public IResult Ok<T>(T data, Pagination.OffsetInfo offsetInfo) =>
        SuccessEnvelopeResultFor(data, offsetInfo, StatusCodes.Status200OK);

    /// <inheritdoc />
    public IResult Ok<T>(T data, Pagination.CursorInfo cursorInfo) =>
        SuccessEnvelopeResultFor(data, cursorInfo, StatusCodes.Status200OK);

    /// <inheritdoc />
    public IResult Ok<T>(T data, Pagination.IPaginationDetails pagination) =>
        SuccessEnvelopeResultFor(data, pagination, StatusCodes.Status200OK);

    private IResult SuccessEnvelopeResultFor<T>(T? data, IPaginationDetails? pagination, int statusCode) =>
        new SuccessEnvelopeResult<T>(data, pagination, statusCode, _options);

    /// <inheritdoc />
    public IResult Created<T>(T data, Func<T, object> idSelector) =>
        new CreatedResult<T>(data, idSelector, _options);

    /// <inheritdoc />
    public IResult Created<T>(T data, string locationUri) =>
        new CreatedResult<T>(data, locationUri, _options);

    /// <inheritdoc />
    public IResult NoContent() =>
        Results.NoContent();

    /// <inheritdoc />
    public IResult BadRequest(string? message = null, string? code = null, IReadOnlyDictionary<string, string[]>? errors = null) =>
        Envelope(new FailureDetails(StatusCodes.Status400BadRequest, message, code, errors));

    /// <inheritdoc />
    public IResult Unauthorized(string? message = null, string? code = null) =>
        Envelope(new FailureDetails(StatusCodes.Status401Unauthorized, message, code));

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

        // 3. Unknown failures degrade to a 500 instead of throwing.
        return Envelope(new FailureDetails(StatusCodes.Status500InternalServerError, failure.Message, failure.Code));
    }

    /// <summary>
    /// Builds the error envelope from a failure's HTTP intent, through the configured
    /// <see cref="ErrorOptions"/> (typed envelope) or the default RFC 7807
    /// <see cref="DefaultErrorResponse"/>.
    /// </summary>
    private IResult Envelope(FailureDetails details) =>
        new ErrorEnvelopeResult(details, _options);

    /// <inheritdoc />
    public IResult FromFailureDetails(FailureDetails details) =>
        Envelope(details);
}
