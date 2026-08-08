using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// The library's default envelope for a successful operation. Carries the payload and
/// optional pagination. This is the type documented with
/// <c>.Produces&lt;DefaultSuccessResponse&lt;T&gt;&gt;()</c> — it matches what
/// <c>http.Ok(data)</c> and <c>http.Created(data, ...)</c> serialize when no custom
/// envelope is configured.
///
/// <para>
/// Disable via <c>options.Success.Enabled = false</c> to return the raw payload
/// instead. To use a different shape, implement <see cref="ISuccessEnvelope"/> and
/// register it with <c>options.Success.SetResponse&lt;MySuccess&gt;()</c>.
/// </para>
/// </summary>
public sealed class DefaultSuccessResponse<T> : ISuccessEnvelope<T>
{
    /// <summary>The HTTP status code for this response.</summary>
    public int StatusCode { get; set; }

    /// <summary>The payload.</summary>
    public T? Data { get; set; }

    /// <summary>Pagination metadata when the response is paginated, else <c>null</c>.</summary>
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
