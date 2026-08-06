namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Contract for an error envelope. The status code is always written to the response;
/// additional per-request properties (traceId, path, custom) are added via
/// <see cref="AddProperty"/> at execution time by
/// <see cref="Http.ErrorEnvelopeResult"/>, when the <see cref="Http.HttpContext"/>
/// is available.
/// </summary>
public interface IErrorResponse
{
    /// <summary>HTTP status code for this error response.</summary>
    int StatusCode { get; }

    /// <summary>
    /// Adds a per-request property (e.g. <c>traceId</c>, <c>path</c>) that must appear
    /// in the serialized envelope. Implementations decide where the property lands —
    /// typed fields, extension data, or both.
    /// </summary>
    void AddProperty(string name, object? value);
}
