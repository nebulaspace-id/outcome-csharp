using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Non-generic success envelope for responses with no payload.
/// Serializes as <c>{"statusCode": 200}</c> — no <c>data</c> or <c>pagination</c> field.
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

// ──────────────────────────────────────────────────────────────────────────
// 1‑param generic — non‑paginated responses.  No Pagination property means
// the JSON body is clean: { "statusCode": 200, "data": { ... } }.
// Swagger documents exactly that.
//
// Register: options.Success.SetResponse(typeof(SuccessResponse<>))
// ──────────────────────────────────────────────────────────────────────────

/// <summary>
/// Typed success envelope for a <b>non‑paginated</b> response.  Carries
/// <see cref="StatusCode"/> and <see cref="Data"/> only — no pagination key.
/// Implements <see cref="ISuccessEnvelope{T}"/> so the library builds it per
/// request and Swagger documents <c>Data: Order</c> per endpoint.
/// </summary>
/// <typeparam name="TData">The payload type.</typeparam>
public sealed class SuccessResponse<TData> : ISuccessEnvelope<TData>
{
    /// <summary>HTTP status the response used.</summary>
    public int StatusCode { get; init; }

    /// <summary>The payload — typed per endpoint, so Swagger knows the exact shape.</summary>
    public TData? Data { get; init; }

    /// <summary>Builds the per‑request body (no pagination).</summary>
    public static ISuccessEnvelope<TData> From(TData data, IPaginationDetails? pagination, HttpContext httpContext, int statusCode) =>
        new SuccessResponse<TData>
        {
            StatusCode = statusCode,
            Data = data,
        };
}

// ──────────────────────────────────────────────────────────────────────────
// 2‑param generic — paginated responses.  <typeparamref name="TPage"/> is
// the concrete pagination type (<see cref="OffsetInfo"/> or <see cref="CursorInfo"/> or custom),
// so System.Text.Json and Swagger serialize the full shape — Page, Limit,
// TotalPages, NextCursor, PreviousCursor, etc.
//
// Register: options.Success.SetResponse(typeof(SuccessResponse<,>))
// ──────────────────────────────────────────────────────────────────────────

/// <summary>
/// Typed success envelope for a <b>paginated</b> response.  <typeparamref name="TPage"/>
/// is the concrete pagination type, giving STJ and Swagger the full properties instead
/// of just the three <see cref="IPaginationDetails"/> members.
/// Implements <see cref="ISuccessEnvelope{T}"/>.
/// </summary>
/// <typeparam name="TData">The payload type.</typeparam>
/// <typeparam name="TPage">
/// Concrete pagination type — <see cref="OffsetPagintion"/>, <see cref="CursorPagination"/>, or custom.
/// </typeparam>
public sealed class SuccessResponse<TData, TPage> : ISuccessEnvelope<TData>
{
    /// <summary>HTTP status the response used.</summary>
    public int StatusCode { get; init; }

    /// <summary>The payload — typed per endpoint.</summary>
    public TData? Data { get; init; }

    /// <summary>
    /// Pagination metadata with the full concrete shape.
    /// </summary>
    public TPage? Pagination { get; init; }
    
    /// <summary>Builds the per‑request body with typed pagination.</summary>
    public static ISuccessEnvelope<TData> From(TData data, IPaginationDetails? pagination, HttpContext httpContext, int statusCode) =>
        new SuccessResponse<TData, TPage>
        {
            StatusCode = statusCode,
            Data = data,
            Pagination = pagination is TPage typed ? typed : default,
        };
}
