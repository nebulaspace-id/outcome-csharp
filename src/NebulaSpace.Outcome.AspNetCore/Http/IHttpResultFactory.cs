using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// Strategy abstraction over building HTTP responses from payloads and failures.
/// Registered in DI via <c>AddOutcomeHttp</c>; inject it into endpoints or
/// services instead of calling the old static <see cref="HttpResult"/> helpers.
/// </summary>
public interface IHttpResultFactory
{
    /// <summary>200 OK with a payload, wrapped in <c>DefaultSuccessResponse&lt;T&gt;</c> per <c>SuccessEnvelope.Enabled</c>.</summary>
    IResult Ok<T>(T? data = default);

    /// <summary>200 OK with a payload and pagination metadata.</summary>
    IResult Ok<T>(T data, Pagination.PageInfo pageInfo);

    /// <summary>201 Created with a <c>Location</c> header derived from the request path + id.</summary>
    IResult Created<T>(T data, Func<T, object> idSelector);

    /// <summary>204 No Content.</summary>
    IResult NoContent();

    /// <summary>400 Bad Request with an error envelope.</summary>
    IResult BadRequest(string? message = null, string? code = null, IReadOnlyDictionary<string, string[]>? errors = null);

    /// <summary>401 Unauthorized.</summary>
    IResult Unauthorized(string? message = null, string? code = null);

    /// <summary>403 Forbidden.</summary>
    IResult Forbidden(string? message = null, string? code = null);

    /// <summary>404 Not Found.</summary>
    IResult NotFound(string? message = null, string? code = null);

    /// <summary>409 Conflict.</summary>
    IResult Conflict(string? message = null, string? code = null);

    /// <summary>
    /// Maps a <see cref="Failure"/> to its HTTP response. An explicit
    /// <c>options.Failures.Map&lt;T&gt;</c> registration wins over the failure's own
    /// <see cref="IHttpFailure"/> mapping, with a fallback of 500 Internal Server Error
    /// for unknown failures.
    /// </summary>
    IResult FromFailure(Failure failure);

    /// <summary>
    /// Builds an HTTP response from a failure's HTTP intent (<see cref="FailureDetails"/>),
    /// through the configured error envelope. Used by exception mappers
    /// (<c>options.Exceptions.Map&lt;T&gt;</c>) that return intent.
    /// </summary>
    IResult FromFailureDetails(FailureDetails details);
}
