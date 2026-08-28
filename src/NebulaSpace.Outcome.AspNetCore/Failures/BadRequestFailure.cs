using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Failures;

/// <summary>
/// HTTP 400 — the request is malformed. Optionally carries field-level validation errors
/// that become the envelope's <c>errors</c> key (RFC 9457: field name → array of messages).
/// </summary>
public sealed class BadRequestFailure : Failure, IHttpFailure
{
    /// <summary>
    /// Creates a 400 failure.
    /// </summary>
    /// <param name="message">Human-readable description of what was wrong with the request.</param>
    /// <param name="errors">Field-level validation errors (RFC 9457): field name → array of messages. Omit for plain 400s.</param>
    public BadRequestFailure(string message, IReadOnlyDictionary<string, string[]>? errors = null) : base(message, "bad_request")
    {
        Errors = errors;
    }

    /// <summary>Field-level validation errors (RFC 9457): field name → array of messages. <c>null</c> when none.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public FailureDetails ToDetails() => new(StatusCodes.Status400BadRequest, Message, Code, Errors);
}
