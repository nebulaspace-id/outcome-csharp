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
    /// <param name="offsetInfo">Pagination metadata built with <c>OffsetInfo.Create</c>.</param>
    IResult Ok<T>(T data, Pagination.OffsetInfo offsetInfo);

    /// <summary>200 OK with a payload and cursor-pagination metadata.</summary>
    /// <param name="data">The payload.</param>
    /// <param name="cursorInfo">Pagination metadata built with <c>CursorInfo.Create</c>.</param>
    IResult Ok<T>(T data, Pagination.CursorInfo cursorInfo);

    /// <summary>
    /// 200 OK with a payload and <em>any</em> pagination metadata implementing
    /// <see cref="Pagination.IPaginationDetails"/>. Use this for custom pagination types
    /// (keyset, time‑based, etc.) — the pipeline resolves the concrete type at runtime so
    /// <c>System.Text.Json</c> and Swagger serialize the full shape.
    /// </summary>
    /// <param name="data">The payload.</param>
    /// <param name="pagination">
    /// Pagination metadata — <see cref="Pagination.OffsetInfo"/>,
    /// <see cref="Pagination.CursorInfo"/>, or any custom type implementing
    /// <see cref="Pagination.IPaginationDetails"/>.
    /// </param>
    IResult Ok<T>(T data, Pagination.IPaginationDetails pagination);

    /// <summary>
    /// 201 Created with a <c>Location</c> header derived from the current request path
    /// plus the new entity's id. The body is wrapped in the configured success envelope.
    /// </summary>
    /// <param name="data">The new entity.</param>
    /// <param name="idSelector">A function that extracts the id from the entity (e.g. <c>o => o.Id</c>).</param>
    IResult Created<T>(T data, Func<T, object> idSelector);

    /// <summary>
    /// 201 Created with a manually specified <c>Location</c> header. Use when the
    /// resource URI doesn't follow the <c>{requestPath}/{id}</c> convention — e.g.
    /// nested routes, absolute URLs, or cross-resource references.
    /// </summary>
    /// <param name="data">The new entity.</param>
    /// <param name="locationUri">The full <c>Location</c> header value (e.g. <c>"/orders/5/items/42"</c>).</param>
    IResult Created<T>(T data, string locationUri);

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
