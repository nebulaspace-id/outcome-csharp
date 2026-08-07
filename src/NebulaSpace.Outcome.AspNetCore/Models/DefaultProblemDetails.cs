namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// The library's default error envelope: a clean RFC 7807 problem response.
///
/// The serialized body IS this type — <c>title</c>/<c>status</c>/<c>detail</c>, all
/// non-nullable so the OpenAPI schema is clean (no <c>nullable</c> flags). No
/// <c>type</c>, <c>traceId</c>, <c>path</c>, <c>statusCode</c>, or <c>errorCode</c>
/// unless configured. To use a different shape, set any type (no inheritance required)
/// via <c>options.ErrorEnvelope.Set&lt;T&gt;</c>.
///
/// The HTTP status is written from the failure's <see cref="FailureDetails.StatusCode"/>,
/// not from this type — so the body here is pure representation.
/// </summary>
public sealed class DefaultProblemDetails
{
    /// <summary>Short, human-readable summary of the problem.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>HTTP status code generated for this occurrence of the problem.</summary>
    public int Status { get; set; }

    /// <summary>Human-readable explanation specific to this occurrence.</summary>
    public string Detail { get; set; } = string.Empty;
}
