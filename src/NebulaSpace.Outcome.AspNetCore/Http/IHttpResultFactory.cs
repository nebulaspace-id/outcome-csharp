using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// Strategy abstraction over building HTTP responses from payloads and failures.
/// Registered in DI via <c>AddOutcome</c>; inject it into endpoints or services
/// instead of calling the old static helpers.
///
/// <para>
/// Because it is resolved through DI, the factory honors the <see cref="OutcomeOptions"/>
/// registered for <em>that</em> application — no shared static state. Each host can
/// configure the library independently.
/// </para>
/// </summary>
public interface IHttpResultFactory
{
    /// <summary>200 OK with no payload.</summary>
    IResult Ok();

    /// <summary>
    /// 200 OK with a payload, wrapped in the configured success envelope per
    /// <c>options.Success.Enabled</c>.
    /// </summary>
    /// <param name="data">The payload. Pass <c>default</c> for a <c>Unit</c> response.</param>
    IResult Ok<T>(T? data = default);

    /// <summary>200 OK with a payload and offset-pagination metadata.</summary>
    /// <param name="data">The payload.</param>
    /// <param name="offsetInfo">Pagination metadata built with <c>DefaultOffsetInfo.Create</c>.</param>
    IResult Ok<T>(T data, Pagination.DefaultOffsetInfo offsetInfo);

    /// <summary>200 OK with a payload and cursor-pagination metadata.</summary>
    /// <param name="data">The payload.</param>
    /// <param name="cursorInfo">Pagination metadata built with <c>DefaultCursorInfo.Create</c>.</param>
    IResult Ok<T>(T data, Pagination.DefaultCursorInfo cursorInfo);

    /// <summary>
    /// 200 OK with a payload and <em>any</em> pagination metadata implementing
    /// <see cref="Pagination.IPaginationDetails"/>. Use this for custom pagination types
    /// (keyset, time‑based, etc.) — the pipeline resolves the concrete type at runtime so
    /// <c>System.Text.Json</c> and Swagger serialize the full shape.
    /// </summary>
    /// <param name="data">The payload.</param>
    /// <param name="pagination">
    /// Pagination metadata — <see cref="Pagination.DefaultOffsetInfo"/>,
    /// <see cref="Pagination.DefaultCursorInfo"/>, or any custom type implementing
    /// <see cref="Pagination.IPaginationDetails"/>.
    /// </param>
    IResult Ok<T>(T data, Pagination.IPaginationDetails pagination);

    /// <summary>
    /// 201 Created with a manually specified <c>Location</c> header and response body.
    /// Matches the standard ASP.NET Core <c>Results.Created(uri, value)</c> signature.
    /// </summary>
    /// <param name="locationUri">The full <c>Location</c> header value (e.g. <c>"/products/42"</c>).</param>
    /// <param name="data">The new entity.</param>
    IResult Created<T>(string locationUri, T data);

    /// <summary>204 No Content — no body, no envelope.</summary>
    IResult NoContent();

    /// <summary>
    /// 400 Bad Request with the configured error envelope.
    /// </summary>
    /// <param name="message">Human‑readable message, if any.</param>
    /// <param name="code">Machine‑readable code, if any.</param>
    /// <param name="errors">Field‑level validation errors (RFC 9457), if any.</param>
    IResult BadRequest(string? message = null, string? code = null, IReadOnlyDictionary<string, string[]>? errors = null);

    /// <summary>401 Unauthorized with the configured error envelope.</summary>
    IResult Unauthorized(string? message = null, string? code = null);

    /// <summary>403 Forbidden with the configured error envelope.</summary>
    IResult Forbidden(string? message = null, string? code = null);

    /// <summary>404 Not Found with the configured error envelope.</summary>
    IResult NotFound(string? message = null, string? code = null);

    /// <summary>409 Conflict with the configured error envelope.</summary>
    IResult Conflict(string? message = null, string? code = null);

    /// <summary>
    /// Maps a <see cref="Failure"/> to its HTTP response. Resolution order:
    /// <list type="number">
    /// <item>An explicit <c>options.Error.MapFailure&lt;T&gt;</c> registration wins above all.</item>
    /// <item>The failure's own <see cref="IHttpFailure.ToDetails"/> mapping.</item>
    /// <item>Unknown failures degrade to 500 Internal Server Error (never throws).</item>
    /// </list>
    /// </summary>
    /// <param name="failure">The failure to map. Subclasses inherit their nearest ancestor's mapping.</param>
    IResult FromFailure(Failure failure);

    /// <summary>
    /// Builds an HTTP response from a failure's HTTP intent (<see cref="FailureDetails"/>),
    /// through the configured error envelope. Used by exception mappers
    /// (<c>options.Error.MapException&lt;T&gt;</c>) that return intent rather than a body.
    /// </summary>
    /// <param name="details">The failure's HTTP intent (status, message, code, errors).</param>
    IResult FromFailureDetails(FailureDetails details);
}
