namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Describes a failure's HTTP intent: the status code, a human-readable message,
/// a machine-readable code, and optional field errors. This type is deliberately
/// free of any response shape — it is the raw material an
/// <see cref="IErrorResponseFactory"/> turns into a concrete envelope.
/// </summary>
public sealed class FailureDetails
{
    public FailureDetails(int statusCode, string? message = null, string? code = null, IReadOnlyDictionary<string, string>? errors = null)
    {
        StatusCode = statusCode;
        Message = message;
        Code = code;
        Errors = errors;
    }

    /// <summary>HTTP status code for this failure.</summary>
    public int StatusCode { get; }

    /// <summary>Human-readable message, if any.</summary>
    public string? Message { get; }

    /// <summary>Machine-readable code (e.g. <c>"not_found"</c>), if any.</summary>
    public string? Code { get; }

    /// <summary>Field-level validation errors, if any.</summary>
    public IReadOnlyDictionary<string, string>? Errors { get; }
}
