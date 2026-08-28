using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Failures;

/// <summary>
/// HTTP 400 with field-level validation errors (RFC 9457). Use when the request failed
/// validation — the <see cref="Errors"/> map becomes the envelope's <c>errors</c> key:
/// <c>{ "fieldName": ["message 1", "message 2"] }</c>.
/// </summary>
public sealed class ValidationFailure : Failure, IHttpFailure
{
    /// <summary>
    /// Creates a 400 validation failure.
    /// </summary>
    /// <param name="message">Human-readable summary of the validation problem.</param>
    /// <param name="errors">Field-level validation errors (RFC 9457): field name → array of messages. Required.</param>
    public ValidationFailure(string message, IReadOnlyDictionary<string, string[]> errors) : base(message, "validation")
    {
        Errors = errors;
    }

    /// <summary>Field-level validation errors: field name → array of messages.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public FailureDetails ToDetails() => new(StatusCodes.Status400BadRequest, Message, Code, Errors);
}
