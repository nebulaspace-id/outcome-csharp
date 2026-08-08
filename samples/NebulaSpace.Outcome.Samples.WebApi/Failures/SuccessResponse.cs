using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Failures;

/// <summary>
/// The sample app's AOT-safe non-generic success body. The serialized body IS this type,
/// so <c>.Produces&lt;SuccessResponse&gt;()</c> documents the exact success shape.
/// Implements <see cref="ISuccessEnvelope"/> so the library can build it; registered
/// once via <c>options.Success.SetResponse&lt;SuccessResponse&gt;()</c>.
/// The payload is <c>object?</c> — in Swagger <c>data</c> shows as <c>object</c>.
/// For per-endpoint typed data in OpenAPI, use the open-generic
/// <c>options.Success.SetResponse(typeof(SuccessResponse&lt;&gt;))</c> instead.
/// </summary>
public sealed class SuccessResponse : ISuccessEnvelope
{
    /// <summary>HTTP status the response used (<c>200</c> for <c>Ok</c>, <c>201</c> for <c>Created</c>).</summary>
    public int StatusCode { get; init; }

    /// <summary>The payload — serialized with its concrete runtime type.</summary>
    public object? Data { get; init; }

    /// <summary>Pagination metadata when the response is paginated.</summary>
    public PageInfo? Pagination { get; init; }

    /// <summary>Builds the per-request success body from the response intent and the current request.</summary>
    public static ISuccessEnvelope From(SuccessDetails details, HttpContext httpContext) =>
        new SuccessResponse
        {
            StatusCode = details.StatusCode,
            Data = details.Data,
            Pagination = details.Pagination,
        };
}