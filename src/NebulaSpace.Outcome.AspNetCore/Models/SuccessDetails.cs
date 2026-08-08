using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Describes a success response's intent: the payload, optional pagination, and the HTTP
/// status code. This type is deliberately free of any response shape — it is the raw
/// material the configured success envelope (<c>options.Success.SetResponse&lt;T&gt;()</c>)
/// renders into a concrete body. Mirrors <see cref="FailureDetails"/>.
/// </summary>
public sealed class SuccessDetails
{
    /// <summary>
    /// Creates a new success intent.
    /// </summary>
    /// <param name="data">The payload.</param>
    /// <param name="pagination">Pagination metadata when the response is paginated, else <c>null</c>.</param>
    /// <param name="statusCode">HTTP status code (<c>200</c> for <c>Ok</c>, <c>201</c> for <c>Created</c>).</param>
    public SuccessDetails(object? data, PageInfo? pagination, int statusCode)
    {
        Data = data;
        Pagination = pagination;
        StatusCode = statusCode;
    }

    /// <summary>The payload.</summary>
    public object? Data { get; }

    /// <summary>Pagination metadata when the response is paginated, else <c>null</c>.</summary>
    public PageInfo? Pagination { get; }

    /// <summary>HTTP status code for the response (<c>200</c> for <c>Ok</c>, <c>201</c> for <c>Created</c>).</summary>
    public int StatusCode { get; }
}
