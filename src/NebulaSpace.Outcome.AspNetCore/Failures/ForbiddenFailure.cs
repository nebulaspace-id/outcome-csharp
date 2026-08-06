using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Failures;

/// <summary>HTTP 403 — the caller is not allowed to perform this operation.</summary>
public sealed class ForbiddenFailure : Failure, IHttpFailure
{
    public ForbiddenFailure(string message) : base(message, "forbidden")
    {
    }

    public FailureDetails ToDetails() => new(StatusCodes.Status403Forbidden, Message, Code);
}
