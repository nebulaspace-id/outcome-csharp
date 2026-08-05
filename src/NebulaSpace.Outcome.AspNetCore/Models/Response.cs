using Microsoft.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Common envelope fields shared by success and error responses.
/// </summary>
public abstract class ResponseBody
{
    public int StatusCode { get; set; }
}

/// <summary>
/// Envelope for a successful operation. Carries the payload and optional pagination.
/// </summary>
public sealed class SuccessResponse<T> : ResponseBody
{
    public T? Data { get; set; }
    public Pagination? Pagination { get; set; }

    public static SuccessResponse<T> Ok(T? data, Pagination? pagination = null) => new()
    {
        StatusCode = StatusCodes.Status200OK,
        Data = data,
        Pagination = pagination,
    };

    internal static SuccessResponse<T> Created(T data) => new()
    {
        StatusCode = StatusCodes.Status201Created,
        Data = data,
    };
}

/// <summary>
/// Envelope for a failure. Carries a message and optional field errors.
/// </summary>
public sealed class ErrorResponse : ResponseBody
{
    public string? TraceId { get; set; }
    public string? Path { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, string>? Errors { get; set; }

    public static ErrorResponse BadRequest(string message, Dictionary<string, string>? errors = null) => new()
    {
        StatusCode = StatusCodes.Status400BadRequest,
        Message = message,
        Errors = errors,
    };

    public static ErrorResponse Unauthorized(string? message = null) => new()
    {
        StatusCode = StatusCodes.Status401Unauthorized,
        Message = message ?? "Unauthorized",
    };

    public static ErrorResponse Forbidden(string? message = null) => new()
    {
        StatusCode = StatusCodes.Status403Forbidden,
        Message = message ?? "Forbidden",
    };

    public static ErrorResponse NotFound(string? message = null) => new()
    {
        StatusCode = StatusCodes.Status404NotFound,
        Message = message ?? "Not found",
    };

    public static ErrorResponse Conflict(string? message = null) => new()
    {
        StatusCode = StatusCodes.Status409Conflict,
        Message = message ?? "Conflict",
    };

    public static ErrorResponse UnprocessableEntity(string message) => new()
    {
        StatusCode = StatusCodes.Status422UnprocessableEntity,
        Message = message,
    };

    public static ErrorResponse InternalServerError() => new()
    {
        StatusCode = StatusCodes.Status500InternalServerError,
        Message = "Internal Server Error",
    };
}
