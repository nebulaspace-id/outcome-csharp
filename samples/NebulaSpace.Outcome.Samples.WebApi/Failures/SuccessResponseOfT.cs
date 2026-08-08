using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Failures;

/// <summary>
/// A typed generic success body — closes T per request so Swagger documents
/// <c>Data: Order</c> rather than <c>Data: object</c>. Implements
/// <see cref="ISuccessEnvelope{T}"/> so the library can build it; registered
/// once via <c>options.Success.SetResponse(typeof(SuccessResponse&lt;&gt;))</c>.
///
/// <para>
/// <b>NOT AOT-safe.</b> This overload uses <c>MakeGenericType</c> + cached
/// <c>MethodInfo.Invoke</c> at runtime. For NativeAOT, use the non‑generic
/// <c>SuccessResponse : ISuccessEnvelope</c> instead (see the commented
/// example in Program.cs).
/// </para>
/// </summary>
public sealed class SuccessResponse<T> : ISuccessEnvelope<T>
{
    /// <summary>HTTP status the response used (<c>200</c> for <c>Ok</c>, <c>201</c> for <c>Created</c>).</summary>
    public int StatusCode { get; init; }

    /// <summary>The payload — typed per endpoint, so Swagger knows the exact shape.</summary>
    public T? Data { get; init; }

    /// <summary>Pagination metadata when the response is paginated.</summary>
    public PageInfo? Pagination { get; init; }

    /// <summary>Builds the per‑request success body from the typed payload, pagination, request, and status.</summary>
    public static ISuccessEnvelope<T> From(T data, PageInfo? pagination, HttpContext httpContext, int statusCode) =>
        new SuccessResponse<T>
        {
            StatusCode = statusCode,
            Data = data,
            Pagination = pagination,
        };
}
