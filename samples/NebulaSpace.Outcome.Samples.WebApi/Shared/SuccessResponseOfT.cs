using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Non-generic success envelope for responses with no payload.
/// Serializes as <c>{"statusCode": 200}</c> — no <c>data</c> or <c>meta</c> field.
/// Used with <c>.Produces&lt;SuccessResponse&gt;()</c> for <c>http.Ok()</c> (no args).
/// Register with <c>options.Success.SetResponse(typeof(SuccessResponse))</c>.
/// </summary>
public sealed class SuccessResponse
{
    /// <summary>HTTP status the response used.</summary>
    public int StatusCode { get; init; }

    /// <summary>Builds the per-request body (no payload).</summary>
    public static object From(HttpContext httpContext, int statusCode) =>
        new SuccessResponse { StatusCode = statusCode };
}

/// <summary>
/// Typed success envelope for a response. Carries <see cref="StatusCode"/>,
/// the typed <see cref="Data"/> per endpoint, and an optional <see cref="Meta"/>
/// — the app's own response metadata — serialized by its runtime type.
/// Implements <see cref="ISuccessEnvelope{T}"/>.
/// </summary>
/// <typeparam name="TData">The payload type.</typeparam>
public sealed class SuccessResponse<TData> : ISuccessEnvelope<TData>
{
    /// <summary>HTTP status the response used.</summary>
    public int StatusCode { get; init; }

    /// <summary>The payload — typed per endpoint, so Swagger knows the exact shape.</summary>
    public TData? Data { get; init; }

    /// <summary>
    /// Response metadata the app attached to this response, or <c>null</c> when
    /// the response carries none. Serialized by its runtime type.
    /// </summary>
    public object? Meta { get; init; }

    /// <summary>Builds the per‑request body.</summary>
    public static ISuccessEnvelope<TData> From(TData data, object? meta, HttpContext httpContext, int statusCode) =>
        new SuccessResponse<TData>
        {
            StatusCode = statusCode,
            Data = data,
            Meta = meta,
        };
}
