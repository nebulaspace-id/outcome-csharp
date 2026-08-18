namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Describes a failure's HTTP intent: the status code, a human‑readable message,
/// a machine‑readable code, and optional field errors. This type is deliberately
/// free of any response shape — it is the raw material the configured error envelope
/// (<c>options.Error.SetResponse(typeof(MyError))</c>) renders into a concrete body.
///
/// <para>
/// <c>FailureDetails</c> separates a failure's <em>meaning</em> from its <em>shape</em>.
/// A <c>NotFoundFailure</c> declares "404 with message X"; the envelope decides whether
/// that becomes <c>{ title, status, detail }</c> or <c>{ code, message, traceId }</c>.
/// </para>
/// </summary>
public sealed class FailureDetails
{
    /// <summary>
    /// Creates a new failure intent.
    /// </summary>
    /// <param name="statusCode">HTTP status code for this failure.</param>
    /// <param name="message">Human‑readable message, if any.</param>
    /// <param name="code">Machine‑readable code (e.g. <c>"not_found"</c>), if any.</param>
    /// <param name="errors">
    /// Field‑level validation errors per RFC 9457: maps each field to an array of
    /// human‑readable messages. One message is a single‑element array, several are a
    /// longer array. Omit for non‑validation errors.
    /// </param>
    public FailureDetails(int statusCode, string? message = null, string? code = null, IReadOnlyDictionary<string, string[]>? errors = null)
    {
        StatusCode = statusCode;
        Message = message;
        Code = code;
        Errors = errors;
    }

    /// <summary>HTTP status code for this failure.</summary>
    public int StatusCode { get; }

    /// <summary>Human‑readable message, if any.</summary>
    public string? Message { get; }

    /// <summary>Machine‑readable code (e.g. <c>"not_found"</c>), if any.</summary>
    public string? Code { get; }

    /// <summary>
    /// Field‑level validation errors, per RFC 9457: maps each field to the array of
    /// human‑readable messages for it. One message is a single‑element array
    /// (<c>["must be positive"]</c>); multiple messages are a longer array.
    /// <c>null</c> when there are no field errors.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }
}
