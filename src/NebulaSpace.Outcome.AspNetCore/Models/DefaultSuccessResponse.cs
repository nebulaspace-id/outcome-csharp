using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// The library's default envelope for a successful operation. Carries the payload and
/// optional pagination. This is the type documented with
/// <c>.Produces&lt;DefaultSuccessResponse&lt;T&gt;&gt;()</c> — it matches what
/// <c>http.Ok(data)</c> and <c>http.Created(data, ...)</c> serialize. Disable via
/// <c>options.SuccessEnvelope.Enabled = false</c> to return the raw payload instead.
/// </summary>
public sealed class DefaultSuccessResponse<T> : ISuccessEnvelope<T>
{
    public int StatusCode { get; set; }
    public T? Data { get; set; }
    public PageInfo? Pagination { get; set; }

    /// <inheritdoc />
    public static ISuccessEnvelope<T> From(T data, PageInfo? pagination, HttpContext httpContext, int statusCode) =>
        new DefaultSuccessResponse<T>
        {
            StatusCode = statusCode,
            Data = data,
            Pagination = pagination,
        };
}
