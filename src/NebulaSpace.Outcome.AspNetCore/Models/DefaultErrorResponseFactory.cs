using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Default <see cref="IErrorResponseFactory"/>: produces a clean RFC 7807
/// <see cref="OutcomeProblemDetails"/> envelope. The body carries only
/// <c>type</c>/<c>title</c>/<c>status</c>/<c>detail</c> — no <c>traceId</c>,
/// <c>path</c>, <c>errorCode</c>, or field errors unless a caller configures them.
///
/// Per SOLID, the intent's message is optional; this factory owns the
/// "default message per status" decision via <see cref="ReasonPhrases.GetReasonPhrase"/>.
/// Replace this registration to add extras (e.g. <c>errorCode</c>, field errors) or to
/// switch to a different error shape.
/// </summary>
public sealed class DefaultErrorResponseFactory : IErrorResponseFactory
{
    public IErrorResponse Create(FailureDetails details) => new OutcomeProblemDetails
    {
        Title = ReasonPhrases.GetReasonPhrase(details.StatusCode), // default per status
        Status = details.StatusCode,
        Detail = details.Message,                                  // optional from intent
    };
}
