using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Configuration for the success envelope: whether successful responses are wrapped, and
/// — if so — in which type. When no envelope is set, successful responses are wrapped in
/// <c>DefaultSuccessResponse&lt;T&gt;</c> (or returned raw when <see cref="Enabled"/> is
/// <c>false</c>).
///
/// Three ways to configure the envelope, in order of preference:
/// <list type="number">
/// <item>
/// <see cref="SetResponse{TEnvelope}"/> — generic, AOT-safe, zero-reflection.
///   <c>TEnvelope</c> must implement <see cref="ISuccessEnvelope"/>.
/// </item>
/// <item>
/// <see cref="SetResponse(Func{object?, PageInfo?, HttpContext, int, object})"/> —
///   delegate overload, AOT-safe, stores the factory as-is.
/// </item>
/// <item>
/// <see cref="SetResponse(Type)"/> — open‑generic path for typed Swagger. Uses
///   <c>MakeGenericType</c> at runtime; <b>not AOT-safe</b>.
/// </item>
/// </list>
/// </summary>
public sealed class SuccessOptions
{
    /// <summary>
    /// Whether successful responses are wrapped in a success envelope. Default <c>true</c>.
    /// When <c>false</c>, <c>http.Ok(data)</c> and <c>http.Created(data, ...)</c> return
    /// the raw payload directly — no wrapper, no <c>statusCode</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Builds the concrete envelope instance from the payload, optional pagination, the
    /// current request, and the HTTP status the response will use (<c>200</c> for
    /// <c>Ok</c>, <c>201</c> for <c>Created</c>). <c>null</c> until a
    /// <c>SetResponse</c> overload is called; when <c>null</c> the library falls back to
    /// <see cref="DefaultSuccessResponse{T}"/>.
    /// </summary>
    internal Func<object?, PageInfo?, HttpContext, int, object>? Factory { get; private set; }

    // ── AOT-safe paths ──────────────────────────────────────────────

    /// <summary>
    /// <b>AOT-safe, zero-reflection.</b> Sets the success envelope to a non‑generic type
    /// implementing <see cref="ISuccessEnvelope"/>. The library dispatches through the
    /// static abstract <c>From(SuccessDetails, HttpContext)</c> directly.
    /// </summary>
    /// <typeparam name="TEnvelope">A type implementing <see cref="ISuccessEnvelope"/>.</typeparam>
    /// <remarks>
    /// Because the envelope is non‑generic, the <c>data</c> field is typed as
    /// <c>object</c> in Swagger. For per‑endpoint typed <c>data</c>, use the open‑generic
    /// <see cref="SetResponse(Type)"/> overload instead (not AOT-safe).
    /// </remarks>
    /// <example>
    /// <code>
    /// options.Success.SetResponse&lt;SuccessResponse&gt;();
    /// </code>
    /// </example>
    public void SetResponse<TEnvelope>() where TEnvelope : ISuccessEnvelope
    {
        Factory = (data, pagination, http, statusCode) =>
            TEnvelope.From(new SuccessDetails(data, pagination, statusCode), http);
    }

    /// <summary>
    /// <b>AOT-safe delegate overload.</b> Stores the factory delegate as‑is — every
    /// request is a plain delegate call, zero reflection. Useful when the envelope
    /// shape is built inline or the constructor needs extra wiring.
    /// </summary>
    /// <param name="factory">
    /// A delegate that builds the response body from the payload, pagination, request,
    /// and status code.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// options.Success.SetResponse((data, pagination, http, statusCode) =>
    ///     new MySuccess { StatusCode = statusCode, Data = data, Pagination = pagination });
    /// </code>
    /// </example>
    public void SetResponse(Func<object?, PageInfo?, HttpContext, int, object> factory)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    // ── Open-generic path (Swagger fidelity, not AOT-safe) ──────────

    /// <summary>
    /// Sets the success envelope to an <b>open generic</b> type whose type parameter is
    /// the payload type — the library closes it per request so Swagger sees
    /// <c>Data: Order</c> rather than <c>Data: object</c>.
    /// <b>This overload uses <c>MakeGenericType</c> + cached <c>MethodInfo.Invoke</c> —
    /// it is NOT AOT-safe.</b> For NativeAOT, use
    /// <see cref="SetResponse{TEnvelope}"/> or the delegate overload.
    /// </summary>
    /// <param name="envelopeType">
    /// An open generic type (e.g. <c>typeof(ApiSuccess&lt;&gt;)</c>) implementing
    /// <see cref="ISuccessEnvelope{T}"/>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The type does not implement <see cref="ISuccessEnvelope{T}"/>, or its
    /// <c>From</c> method is missing or inaccessible.
    /// </exception>
    /// <example>
    /// <code>
    /// // Open generic: T is the payload type, closed per request
    /// options.Success.SetResponse(typeof(ApiSuccess&lt;&gt;));
    ///
    /// // Close per request to build the envelope
    /// var targetType = envelopeType.MakeGenericType(data.GetType());
    /// </code>
    /// </example>
    public void SetResponse(Type envelopeType)
    {
        var contract = FindContract(envelopeType)
            ?? throw new InvalidOperationException(
                $"Success.SetResponse requires the envelope to implement " +
                $"ISuccessEnvelope<T>. '{envelopeType.FullName}' does not.");

        var _ = contract.GetMethod("From", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Success.SetResponse: '{envelopeType.FullName}' implements ISuccessEnvelope<T> " +
                $"but its 'From' method is missing.");

        Factory = (data, pagination, http, statusCode) =>
        {
            var targetType = envelopeType.IsGenericTypeDefinition
                ? envelopeType.MakeGenericType(data?.GetType() ?? typeof(object))
                : envelopeType;

            var method = FromMethodCache.GetOrAdd(targetType, t =>
                t.GetMethod("From", BindingFlags.Public | BindingFlags.Static)!);

            return method.Invoke(null, new object?[] { data, pagination, http, statusCode })!;
        };
    }

    /// <summary>Finds the <c>ISuccessEnvelope&lt;T&gt;</c> contract on the type (or its open generic form).</summary>
    private static Type? FindContract(Type envelopeType)
    {
        if (envelopeType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISuccessEnvelope<>)))
            return envelopeType;

        if (envelopeType.IsGenericTypeDefinition)
        {
            foreach (var candidate in envelopeType.GetInterfaces())
                if (candidate.IsGenericTypeDefinition && candidate.GetGenericTypeDefinition() == typeof(ISuccessEnvelope<>))
                    return candidate;
        }

        return null;
    }

    /// <summary>
    /// Per‑closed‑type <see cref="MethodInfo"/> cache — used only by
    /// <see cref="SetResponse(Type)"/>. <c>GetMethod("From")</c> runs once per payload
    /// type, then <c>Invoke</c> is reused. The dictionary stays small — one entry per
    /// distinct response type across the process lifetime.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, MethodInfo> FromMethodCache = new();
}
