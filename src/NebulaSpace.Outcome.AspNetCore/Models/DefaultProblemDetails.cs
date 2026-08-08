using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// The library's default error envelope: a clean RFC 7807 problem response.
///
/// <para>
/// The serialized body IS this type — <c>title</c>/<c>status</c>/<c>detail</c>, all
/// non‑nullable so the OpenAPI schema is clean (no <c>nullable</c> flags). No
/// <c>type</c>, <c>traceId</c>, <c>path</c>, <c>statusCode</c>, or <c>errorCode</c>
/// unless you configure a typed envelope. To use a different shape, implement
/// <see cref="IErrorEnvelope"/> and register it with
/// <c>options.Error.SetResponse(typeof(MyError))</c>.
/// </para>
///
/// <para>
/// The HTTP status is written from the failure's <see cref="FailureDetails.StatusCode"/>,
/// not from this type — so the body here is pure representation.
/// </para>
/// </summary>
public sealed class DefaultProblemDetails : IErrorEnvelope
{
    /// <summary>Short, human‑readable summary of the problem (e.g. <c>"Not Found"</c>).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>HTTP status code generated for this occurrence of the problem.</summary>
    public int Status { get; set; }

    /// <summary>Human‑readable explanation specific to this occurrence of the problem.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Builds the clean RFC 7807 body from a failure's HTTP intent.</summary>
    public static IErrorEnvelope From(FailureDetails details, HttpContext httpContext) => new DefaultProblemDetails
    {
        Title = ReasonPhrases.GetReasonPhrase(details.StatusCode),
        Status = details.StatusCode,
        Detail = details.Message ?? string.Empty,
    };
}
