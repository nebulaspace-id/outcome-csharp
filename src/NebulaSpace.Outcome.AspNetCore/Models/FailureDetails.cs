namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Describes a failure's HTTP intent: the status code, a human-readable message,
/// a machine-readable code, and optional field errors. This type is deliberately
/// free of any response shape — it is the raw material the configured error envelope
/// (<c>options.ErrorEnvelope.Set&lt;T&gt;</c>) renders into a concrete body.
/// </summary>
public sealed class FailureDetails
{
    public FailureDetails(int statusCode, string? message = null, string? code = null, IReadOnlyDictionary<string, string[]>? errors = null)
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

    /// <summary>
    /// Field-level validation errors, per RFC 9457: maps each field to the array of
    /// human-readable messages for it. One message is a single-element array
    /// (<c>["must be positive"]</c>); multiple messages are a longer array.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }
}
