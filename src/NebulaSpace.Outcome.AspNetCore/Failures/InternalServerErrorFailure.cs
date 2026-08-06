using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Failures;

/// <summary>HTTP 500 — an unexpected server error occurred.</summary>
public sealed class InternalServerErrorFailure : Failure, IHttpFailure
{
    public InternalServerErrorFailure(string message) : base(message, "internal_server_error")
    {
    }

    public FailureDetails ToDetails() => new(StatusCodes.Status500InternalServerError, Message, Code);
}
