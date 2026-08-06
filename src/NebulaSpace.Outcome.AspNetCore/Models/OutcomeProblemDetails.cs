using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// The library's default error envelope: a clean RFC 7807 problem response that
/// implements <see cref="IErrorResponse"/>.
///
/// The serialized body IS this type — <c>title</c>/<c>status</c>/<c>detail</c> only
/// (<c>detail</c> appears only when present). No <c>type</c>, <c>traceId</c>, <c>path</c>,
/// <c>statusCode</c>, or <c>errorCode</c> unless configured. Per-request properties
/// registered via <c>options.ErrorEnvelope.Add</c>
/// are injected at execution time through <see cref="AddProperty"/> and flattened by
/// <c>[JsonExtensionData]</c>, so they appear at the top level of the JSON body.
/// </summary>
public sealed class OutcomeProblemDetails : IErrorResponse
{
    /// <summary>Short, human-readable summary of the problem.</summary>
    public string? Title { get; set; }

    /// <summary>HTTP status code generated for this occurrence of the problem.</summary>
    public int? Status { get; set; }

    /// <summary>Human-readable explanation specific to this occurrence, if any.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }

    /// <summary>
    /// Per-request properties (traceId, path, custom) injected at execution time.
    /// Flattened into the JSON body by <c>[JsonExtensionData]</c>.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object?> Extensions { get; set; } = new();

    /// <summary>
    /// Status code written to the HTTP response. Explicit interface implementation so it
    /// is NOT serialized into the body (RFC 7807 already carries <c>status</c>).
    /// </summary>
    int IErrorResponse.StatusCode => Status ?? StatusCodes.Status500InternalServerError;

    /// <inheritdoc />
    public void AddProperty(string name, object? value) => Extensions[name] = value;
}
