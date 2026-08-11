using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// The library's default envelope for a Successful <b>non‑paginated</b> operation.
/// Carries only the payload and status code — no pagination key in the serialized
/// body.  This is the type documented with
/// <c>.Produces&lt;DefaultSuccessResponse&lt;T&gt;&gt;()</c> for non‑paginated endpoints.
///
/// <para>
/// Disable via <c>options.Success.Enabled = false</c> to return the raw payload
/// instead.  To use a different shape, implement <see cref="ISuccessEnvelope{T}"/>
/// and register it with <c>options.Success.SetResponse(typeof(MySuccess&lt;&gt;))</c>.
/// </para>
/// </summary>
public sealed class DefaultSuccessResponse<T> : ISuccessEnvelope<T>
{
    /// <summary>The HTTP status code for this response.</summary>
    public int StatusCode { get; set; }

    /// <summary>The payload.</summary>
    public T? Data { get; set; }

    /// <inheritdoc />
    public static ISuccessEnvelope<T> From(T data, IPaginationDetails? pagination, HttpContext httpContext, int statusCode) =>
        new DefaultSuccessResponse<T>
        {
            StatusCode = statusCode,
            Data = data,
        };
}

// ──────────────────────────────────────────────────────────────────────────
// 2‑param generic — paginated responses.  TPage is the concrete pagination
// type (OffsetInfo or CursorInfo) so STJ and Swagger serialize the full shape.
// ──────────────────────────────────────────────────────────────────────────

/// <summary>
/// The library's default envelope for a successful <b>paginated</b> operation.
/// <typeparamref name="TPage"/> is the concrete pagination metadata type
/// (<see cref="Pagination.OffsetInfo"/> or <see cref="Pagination.CursorInfo"/>),
/// so System.Text.Json and Swagger serialize the full shape — not just the
/// three <see cref="IPaginationDetails"/> members.
///
/// <para>
/// Register once with
/// <c>options.Success.SetResponse(typeof(DefaultSuccessResponse&lt;,&gt;))</c>.
/// </para>
/// </summary>
/// <typeparam name="TData">The payload type.</typeparam>
/// <typeparam name="TPage">
/// The concrete pagination metadata type — either <see cref="Pagination.OffsetInfo"/>
/// or <see cref="Pagination.CursorInfo"/>.
/// </typeparam>
public sealed class DefaultSuccessResponse<TData, TPage> : ISuccessEnvelope<TData>
{
    /// <summary>The HTTP status code for this response.</summary>
    public int StatusCode { get; set; }

    /// <summary>The payload.</summary>
    public TData? Data { get; set; }

    /// <summary>
    /// Pagination metadata with the full concrete shape — <see cref="Pagination.OffsetInfo"/>
    /// for offset responses, <see cref="Pagination.CursorInfo"/> for cursor responses.
    /// </summary>
    public TPage? Pagination { get; set; }

    /// <inheritdoc />
    public static ISuccessEnvelope<TData> From(TData data, IPaginationDetails? pagination, HttpContext httpContext, int statusCode) =>
        new DefaultSuccessResponse<TData, TPage>
        {
            StatusCode = statusCode,
            Data = data,
            Pagination = pagination is TPage typed ? typed : default,
        };
}
