using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.Samples.WebApi.Failures;

/// <summary>
/// The sample app's user-custom error body. The serialized body IS this type, so
/// <c>.Produces&lt;ErrorResponse&gt;()</c> documents the exact error shape. Implements
/// <see cref="IErrorEnvelope"/> so the library can build it; registered once via
/// <c>options.Error.SetResponse&lt;ErrorResponse&gt;()</c>.
/// </summary>
public sealed class ErrorResponse : IErrorEnvelope
{
    /// <summary>HTTP status (mirrors <see cref="FailureDetails.StatusCode"/>).</summary>
    public int? StatusCode { get; init; }

    /// <summary>Machine-readable error code, if any.</summary>
    public string? Code { get; init; }

    /// <summary>Human-readable error summary, if any.</summary>
    public string? Message { get; init; }

    /// <summary>Per-request correlation id (<see cref="HttpContext.TraceIdentifier"/>).</summary>
    public string? TraceId { get; init; }

    /// <summary>Field-level validation errors (RFC 9457): field name → array of messages.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    /// <summary>Builds the per-request error body from a failure's HTTP intent and the current request.</summary>
    public static IErrorEnvelope From(FailureDetails details, HttpContext context) => new ErrorResponse
    {
        StatusCode = details.StatusCode,
        Code = details.Code,
        Message = details.Message,
        TraceId = context.TraceIdentifier,
        Errors = details.Errors,
    };
}