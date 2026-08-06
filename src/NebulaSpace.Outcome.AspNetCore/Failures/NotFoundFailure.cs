using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Failures;

/// <summary>HTTP 404 — the requested resource does not exist.</summary>
public sealed class NotFoundFailure : Failure, IHttpFailure
{
    public NotFoundFailure(string message) : base(message, "not_found")
    {
    }

    public FailureDetails ToDetails() => new(StatusCodes.Status404NotFound, Message, Code);
}
