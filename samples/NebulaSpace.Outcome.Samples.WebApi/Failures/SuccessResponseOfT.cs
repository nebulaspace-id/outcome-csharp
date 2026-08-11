using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Failures;

// ──────────────────────────────────────────────────────────────────────────
// 1‑param generic — non‑paginated responses.  No Pagination property means
// the JSON body is clean: { "statusCode": 200, "data": { ... } }.
// Swagger documents exactly that.
//
// Register: options.Success.SetResponse(typeof(SuccessResponse<>))
//
// NOT AOT-safe — uses MakeGenericType + cached MethodInfo.Invoke.
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
// the concrete pagination type (<see cref="OffsetInfo"/> or <see cref="CursorInfo"/>),
// so System.Text.Json and Swagger serialize the full shape — Page, Limit,
// TotalPages, NextCursor, PreviousCursor, etc.
//
// Register: options.Success.SetResponse(typeof(SuccessResponse<,>))
//
// NOT AOT-safe — uses MakeGenericType + cached MethodInfo.Invoke.
// ──────────────────────────────────────────────────────────────────────────

/// <summary>
/// Typed success envelope for a <b>paginated</b> response.  <typeparamref name="TPage"/>
/// is the concrete pagination type, giving STJ and Swagger the full properties instead
/// of just the three <see cref="IPaginationDetails"/> members.
/// Implements <see cref="ISuccessEnvelope{T}"/>.
/// </summary>
/// <typeparam name="TData">The payload type.</typeparam>
/// <typeparam name="TPage">
/// Concrete pagination type — <see cref="OffsetInfo"/> or <see cref="CursorInfo"/>.
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
