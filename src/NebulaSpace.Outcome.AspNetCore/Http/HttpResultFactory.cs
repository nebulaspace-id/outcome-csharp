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
    private readonly IErrorResponseFactory _errorResponseFactory;

    public HttpResultFactory(ResultPatternOptions options)
        : this(options, new DefaultErrorResponseFactory())
    {
    }

    public HttpResultFactory(ResultPatternOptions options, IErrorResponseFactory errorResponseFactory)
    {
        _options = options;
        _errorResponseFactory = errorResponseFactory;
    }

    public IResult Ok<T>(T? data = default) =>
        _options.SuccessEnvelope.Enabled
            ? Results.Ok(SuccessResponse<T>.Ok(data))
            : Results.Ok(data);

    public IResult Ok<T>(T data, Pagination.Pagination pagination) =>
        _options.SuccessEnvelope.Enabled
            ? Results.Ok(SuccessResponse<T>.Ok(data, pagination))
            : Results.Ok(data);

    /// <inheritdoc />
    public IResult Created<T>(T data, Func<T, object> idSelector) =>
        new CreatedResult<T>(data, idSelector, _options);

    public IResult NoContent() =>
        Results.NoContent();

    public IResult BadRequest(string? message = null, string? code = null, Dictionary<string, string>? errors = null) =>
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

    /// <inheritdoc />
    public IResult FromEnvelope(IErrorResponse envelope) =>
        Envelope(envelope);

    /// <summary>
    /// Builds the error envelope through the configured <see cref="IErrorResponseFactory"/>
    /// and wraps it so configured properties are injected at execution time.
    /// </summary>
    private IResult Envelope(FailureDetails details) =>
        new ErrorEnvelopeResult(_errorResponseFactory.Create(details), _options);

    /// <summary>
    /// Wraps a concrete envelope (from a custom <c>Failures.Map</c>) so configured
    /// properties are injected at execution time.
    /// </summary>
    private IResult Envelope(IErrorResponse envelope) =>
        new ErrorEnvelopeResult(envelope, _options);
}
