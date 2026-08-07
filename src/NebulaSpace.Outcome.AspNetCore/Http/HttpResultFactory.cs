using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// Default <see cref="IHttpResultFactory"/> that builds ASP.NET Core <see cref="IResult"/>
/// responses from payloads and failures, honoring the configured
/// <see cref="ResultPatternOptions"/> supplied through constructor injection.
/// </summary>
public sealed class HttpResultFactory : IHttpResultFactory
{
    private readonly ResultPatternOptions _options;

    public HttpResultFactory(ResultPatternOptions options)
    {
        _options = options;
    }

    public IResult Ok<T>(T? data = default) =>
        SuccessEnvelopeResultFor(data, pagination: null, StatusCodes.Status200OK);

    public IResult Ok<T>(T data, Pagination.PageInfo pageInfo) =>
        SuccessEnvelopeResultFor(data, pageInfo, StatusCodes.Status200OK);

    private IResult SuccessEnvelopeResultFor<T>(T? data, Pagination.PageInfo? pagination, int statusCode) =>
        new SuccessEnvelopeResult<T>(data, pagination, statusCode, _options);

    /// <inheritdoc />
    public IResult Created<T>(T data, Func<T, object> idSelector) =>
        new CreatedResult<T>(data, idSelector, _options);

    public IResult NoContent() =>
        Results.NoContent();

    public IResult BadRequest(string? message = null, string? code = null, IReadOnlyDictionary<string, string[]>? errors = null) =>
        Envelope(new FailureDetails(StatusCodes.Status400BadRequest, message, code, errors));

    public IResult Unauthorized(string? message = null, string? code = null) =>
        Envelope(new FailureDetails(StatusCodes.Status401Unauthorized, message, code));

    public IResult Forbidden(string? message = null, string? code = null) =>
        Envelope(new FailureDetails(StatusCodes.Status403Forbidden, message, code));

    public IResult NotFound(string? message = null, string? code = null) =>
        Envelope(new FailureDetails(StatusCodes.Status404NotFound, message, code));

    public IResult Conflict(string? message = null, string? code = null) =>
        Envelope(new FailureDetails(StatusCodes.Status409Conflict, message, code));

    public IResult FromFailure(Failure failure)
    {
        // 1. An explicit mapping registered via options.Failures.Map<TFailure> wins over a
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
    /// <see cref="ErrorEnvelopeOptions"/> (typed envelope) or the default RFC 7807
    /// <see cref="DefaultProblemDetails"/>.
    /// </summary>
    private IResult Envelope(FailureDetails details) =>
        new ErrorEnvelopeResult(details, _options);

    /// <inheritdoc />
    public IResult FromFailureDetails(FailureDetails details) =>
        Envelope(details);
}
