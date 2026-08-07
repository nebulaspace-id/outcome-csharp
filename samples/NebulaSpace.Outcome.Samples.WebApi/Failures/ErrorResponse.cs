using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.Samples.WebApi.Failures;

/// <summary>
/// The sample app's user-custom error body. Inherits nothing from the library — the
/// serialized body IS this type, so <c>.Produces&lt;ErrorResponse&gt;()</c> documents the
/// exact error shape. <see cref="From"/> builds it from a failure's HTTP intent
/// (<see cref="FailureDetails"/>) plus the current request; registered once via
/// <c>options.ErrorEnvelope.Set(ErrorResponse.From)</c>.
/// </summary>
public sealed class ErrorResponse
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
    public static ErrorResponse From(FailureDetails details, HttpContext context) => new()
    {
        StatusCode = details.StatusCode,
        Code = details.Code,
        Message = details.Message,
        TraceId = context.TraceIdentifier,
        Errors = details.Errors,
    };
}