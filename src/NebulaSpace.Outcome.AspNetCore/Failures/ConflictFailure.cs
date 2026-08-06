using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Failures;

/// <summary>HTTP 409 — the resource is in a conflicting state.</summary>
public sealed class ConflictFailure : Failure, IHttpFailure
{
    public ConflictFailure(string message) : base(message, "conflict")
    {
    }

    public FailureDetails ToDetails() => new(StatusCodes.Status409Conflict, Message, Code);
}
