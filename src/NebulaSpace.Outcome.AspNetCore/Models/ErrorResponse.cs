using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Envelope for a failure. Carries a message, the failure's machine-readable
/// <see cref="ErrorCode"/>, and optional field errors. Per-request envelope
/// properties (traceId, path, custom) are injected via <see cref="ExtensionData"/>
/// by <see cref="Http.ErrorEnvelopeResult"/> at execution time.
/// </summary>
public sealed class ErrorResponse : ResponseBody, IErrorResponse
{
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public Dictionary<string, string>? Errors { get; set; }

    /// <summary>
    /// Arbitrary per-request properties (defaults: <c>traceId</c>, <c>path</c>)
    /// injected into the JSON envelope by <see cref="Http.ErrorEnvelopeResult"/>
    /// via <see cref="AddProperty"/>.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    /// <inheritdoc />
    public void AddProperty(string name, object? value)
    {
        ExtensionData ??= new Dictionary<string, JsonElement>();
        ExtensionData[name] = JsonSerializer.SerializeToElement(value);
    }

    public static ErrorResponse BadRequest(string message, string? code = null, Dictionary<string, string>? errors = null) => new()
    {
        StatusCode = StatusCodes.Status400BadRequest,
        Message = message,
        ErrorCode = code,
        Errors = errors,
    };

    public static ErrorResponse Unauthorized(string? message = null, string? code = null) => new()
    {
        StatusCode = StatusCodes.Status401Unauthorized,
        Message = message ?? "Unauthorized",
        ErrorCode = code,
    };

    public static ErrorResponse Forbidden(string? message = null, string? code = null) => new()
    {
        StatusCode = StatusCodes.Status403Forbidden,
        Message = message ?? "Forbidden",
        ErrorCode = code,
    };

    public static ErrorResponse NotFound(string? message = null, string? code = null) => new()
    {
        StatusCode = StatusCodes.Status404NotFound,
        Message = message ?? "Not found",
        ErrorCode = code,
    };

    public static ErrorResponse Conflict(string? message = null, string? code = null) => new()
    {
        StatusCode = StatusCodes.Status409Conflict,
        Message = message ?? "Conflict",
        ErrorCode = code,
    };

    public static ErrorResponse UnprocessableEntity(string message, string? code = null) => new()
    {
        StatusCode = StatusCodes.Status422UnprocessableEntity,
        Message = message,
        ErrorCode = code,
    };

    public static ErrorResponse InternalServerError(string? message = null, string? code = null) => new()
    {
        StatusCode = StatusCodes.Status500InternalServerError,
        Message = message ?? "Internal Server Error",
        ErrorCode = code,
    };
}
