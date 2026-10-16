using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Typed success envelope for a response. Carries <see cref="StatusCode"/>,
/// the typed <see cref="Data"/> per endpoint, and <see cref="Meta"/>.
/// Implements <see cref="ISuccessEnvelope{T}"/>.
///
/// <para>
/// <see cref="Meta"/> is the app's own type, not <see cref="object"/>: this API has one
/// meta shape for the whole surface, so the envelope names it and Swagger documents the
/// exact fields. The library still hands meta over as <see cref="object"/> — it must not
/// know the app's meta type — so the envelope narrows it here.
/// </para>
///
/// <para>
/// Whether every response carries meta is the app's call, not the library's. Two shapes,
/// one line apart in <see cref="From"/>:
/// </para>
///
/// <code>
/// // (a) opt-in — meta appears only where an endpoint built one.
/// //     http.Ok(product)  → { "statusCode": 200, "data": { ... } }
/// //     http.Ok(p, meta)  → { "statusCode": 200, "data": ..., "meta": { ... } }
/// Meta = meta as Meta,
///
/// // (b) always-on — every response carries a timestamp, and only pagination is opt-in.
/// //     http.Ok(product)  → { "statusCode": 200, "data": ..., "meta": { ... } }
/// Meta = meta as Meta ?? new Meta(),
/// </code>
///
/// <para>
/// This sample uses (b), but nothing in the library depends on that choice — it only
/// requires the <see cref="ISuccessEnvelope{T}"/> contract.
/// </para>
/// </summary>
/// <typeparam name="TData">The payload type.</typeparam>
public sealed class SuccessResponse<TData> : ISuccessEnvelope<TData>
{
    /// <summary>HTTP status the response used.</summary>
    public int StatusCode { get; init; }

    /// <summary>The payload — typed per endpoint, so Swagger knows the exact shape.</summary>
    public TData? Data { get; init; }

    /// <summary>
    /// Response metadata for this response — the app's own when the endpoint attached one,
    /// otherwise a bare timestamp. Drop the fallback in <see cref="From"/> to make it
    /// strictly opt-in.
    /// </summary>
    public Meta? Meta { get; init; }

    /// <summary>Builds the per‐request body.</summary>
    public static ISuccessEnvelope<TData> From(TData data, object? meta, HttpContext httpContext, int statusCode) =>
        new SuccessResponse<TData>
        {
            StatusCode = statusCode,
            Data = data,
            // The library carries meta as object; this API has exactly one meta type, so
            // narrow it. A null (endpoint that attached no meta) falls back to a bare
            // timestamp — every response carries meta, so endpoints opt into pagination,
            // not into meta. Drop the "?? new Meta()" to make meta opt-in instead.
            Meta = meta as Meta ?? new Meta(),
        };
}
