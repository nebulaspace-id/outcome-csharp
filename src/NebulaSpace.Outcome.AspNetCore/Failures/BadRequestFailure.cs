using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Failures;

/// <summary>HTTP 400 — the request is malformed.</summary>
public sealed class BadRequestFailure : Failure, IHttpFailure
{
    public BadRequestFailure(string message, IReadOnlyDictionary<string, string[]>? errors = null) : base(message, "bad_request")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public FailureDetails ToDetails() => new(StatusCodes.Status400BadRequest, Message, Code, Errors);
}
