using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Failures;

/// <summary>
/// The sample app's user-custom success body. The serialized body IS this type, so
/// <c>.Produces&lt;SuccessResponse&lt;T&gt;&gt;()</c> documents the exact success shape.
/// Implements <see cref="ISuccessEnvelope{T}"/> so the library can build it; registered
/// once via <c>options.SuccessEnvelope.Set(typeof(SuccessResponse&lt;&gt;))</c>.
/// </summary>
public sealed class SuccessResponse<T> : ISuccessEnvelope<T>
{
    /// <summary>HTTP status the response used (<c>200</c> for <c>Ok</c>, <c>201</c> for <c>Created</c>).</summary>
    public int StatusCode { get; init; }

    /// <summary>The payload.</summary>
    public T? Data { get; init; }

    /// <summary>Pagination metadata when the response is paginated.</summary>
    public PageInfo? Pagination { get; init; }

    /// <summary>Builds the per-request success body from the payload, pagination, request, and status.</summary>
    public static ISuccessEnvelope<T> From(T data, PageInfo? pagination, HttpContext httpContext, int statusCode) =>
        new SuccessResponse<T>
        {
            StatusCode = statusCode,
            Data = data,
            Pagination = pagination,
        };
}