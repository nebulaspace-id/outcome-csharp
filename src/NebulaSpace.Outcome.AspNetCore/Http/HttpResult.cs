using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Failures;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Http;

public static class HttpResult
{
    public static IResult Ok<T>(T? data = default) =>
        ResultPattern.Options.EnvelopeSuccess
            ? Results.Ok(SuccessResponse<T>.Ok(data))
            : Results.Ok(data);

    public static IResult Ok<T>(T data, Pagination pagination) =>
        ResultPattern.Options.EnvelopeSuccess
            ? Results.Ok(SuccessResponse<T>.Ok(data, pagination))
            : Results.Ok(data);

    /// <summary>
    /// Returns 201 Created, building the Location header from the current request
    /// path plus the new entity's id (e.g. <c>POST /orders</c> → <c>/orders/{id}</c>).
    /// </summary>
    public static IResult Created<T>(T data, Func<T, object> idSelector) =>
        new CreatedResult<T>(data, idSelector);

    public static IResult NoContent() =>
        Results.NoContent();

    public static IResult BadRequest(string? message = null, Dictionary<string, string>? errors = null) =>
        Results.BadRequest(ErrorResponse.BadRequest(message ?? "Bad Request", errors));

    public static IResult Unauthorized() =>
        Results.Json(ErrorResponse.Unauthorized(), statusCode: StatusCodes.Status401Unauthorized);

    public static IResult Forbidden(string? message = null) =>
        Results.Json(ErrorResponse.Forbidden(message), statusCode: StatusCodes.Status403Forbidden);

    public static IResult NotFound(string? message = null) =>
        Results.NotFound(ErrorResponse.NotFound(message));

    public static IResult Conflict(string? message = null) =>
        Results.Conflict(ErrorResponse.Conflict(message));

    public static IResult FromFailure(Failure failure)
    {
        // Application-defined failures registered via MapFailure<TFailure>.
        if (ResultPattern.Options.CustomFailureMappers.TryGetValue(failure.GetType(), out var custom))
        {
            return custom(failure);
        }

        return failure switch
        {
            NotFoundFailure f => Results.NotFound(ErrorResponse.NotFound(f.Message)),
            ValidationFailure f => Results.BadRequest(ErrorResponse.BadRequest(f.Message, f.Errors)),
            ConflictFailure f => Results.Conflict(ErrorResponse.Conflict(f.Message)),
            BadRequestFailure f => Results.BadRequest(ErrorResponse.BadRequest(f.Message, f.Errors)),
            UnauthorizedFailure => Results.Json(ErrorResponse.Unauthorized(), statusCode: StatusCodes.Status401Unauthorized),
            ForbiddenFailure f => Results.Json(ErrorResponse.Forbidden(f.Message), statusCode: StatusCodes.Status403Forbidden),
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "Unknown failure type"),
        };
    }
}
