using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Failures;

/// <summary>HTTP 400 with field-level validation errors (RFC 9457: field → string[]).</summary>
public sealed class ValidationFailure : Failure, IHttpFailure
{
    public ValidationFailure(string message, IReadOnlyDictionary<string, string[]> errors) : base(message, "validation")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public FailureDetails ToDetails() => new(StatusCodes.Status400BadRequest, Message, Code, Errors);
}
