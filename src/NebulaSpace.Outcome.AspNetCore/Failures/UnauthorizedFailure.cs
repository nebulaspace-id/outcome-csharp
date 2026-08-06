using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Failures;

/// <summary>HTTP 401 — authentication is required or failed.</summary>
public sealed class UnauthorizedFailure : Failure, IHttpFailure
{
    public UnauthorizedFailure(string message) : base(message, "unauthorized")
    {
    }

    public FailureDetails ToDetails() => new(StatusCodes.Status401Unauthorized, Message, Code);
}
