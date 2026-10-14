using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Configuration for the success envelope: whether successful responses are wrapped, and
/// — if so — in which type. When no envelope is set, successful responses are wrapped in
/// <c>DefaultSuccessResponse&lt;T&gt;</c> (or returned raw when <see cref="Enabled"/> is
/// <c>false</c>).
///
/// <para>
/// The envelope receives the app's response metadata as an <see cref="object"/> — the
/// metadata's type is the app's business and the library never inspects it.
/// </para>
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
    /// Builds the concrete envelope instance from the payload, the app's metadata, the
    /// current request, and the HTTP status. <c>null</c> until a <c>SetResponse</c>
    /// overload is called; when <c>null</c> the library falls back to
    /// <see cref="DefaultSuccessResponse{T}"/>.
    /// </summary>
    internal Func<object?, object?, HttpContext, int, object>? Factory { get; private set; }

    /// <summary>
    /// Builds the concrete envelope instance for responses with no payload
    /// (<c>http.Ok()</c> with no arguments). When <c>null</c>, the library uses
    /// <see cref="DefaultSuccessResponse"/> — a status-code-only envelope.
    /// </summary>
    internal Func<HttpContext, int, object>? EmptyFactory { get; private set; }

    /// <summary>
    /// Stores the factory delegate as‑is — every request is a plain delegate call, zero
    /// reflection. Useful when the envelope shape is built inline or the constructor needs
    /// extra wiring, and AOT‑safe.
    /// </summary>
    /// <param name="factory">
    /// A delegate that builds the response body from the payload, the app's metadata,
    /// the request, and the status code.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <c>null</c>.</exception>
    public void SetResponse(Func<object?, object?, HttpContext, int, object> factory)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Sets the success envelope for responses with no payload (<c>http.Ok()</c> with no
    /// arguments). The delegate receives the HTTP context and status code.
    /// </summary>
    /// <param name="factory">A delegate that builds the response body.</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <c>null</c>.</exception>
    public void SetResponse(Func<HttpContext, int, object> factory)
    {
        EmptyFactory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Sets the success envelope by <see cref="Type"/>. Pass an <b>open generic</b> for
    /// payload envelopes, or a <b>non-generic</b> type for no-payload envelopes
    /// (<c>http.Ok()</c> with no arguments).
    ///
    /// <para>
    /// <b>Open generic</b> — the type's single type parameter is closed with the payload
    /// type per request, so Swagger sees <c>Data: Order</c> rather than <c>Data: object</c>.
    /// The app's metadata arrives in the envelope as an <see cref="object"/> and needs no
    /// type parameter of its own.
    /// </para>
    ///
    /// <para>
    /// <b>Non-generic</b> — pass a non-generic type for no-payload responses
    /// (<c>http.Ok()</c> with no arguments). The type must have a
    /// <c>public static From(HttpContext, int)</c> method. No
    /// <see cref="ISuccessEnvelope{T}"/> contract is required.
    /// </para>
    ///
    /// <para>
    /// <b>Why <c>Type</c>, not a generic?</b> Error's <see cref="ErrorOptions.SetResponse{TEnvelope}"/>
    /// is a compile‑time generic because <see cref="IErrorEnvelope"/> is non‑generic — there is
    /// only one type to specify, so <c>SetResponse&lt;ErrorResponse&gt;()</c> works directly.
    /// Success uses a runtime <see cref="Type"/> parameter because
    /// <see cref="ISuccessEnvelope{T}"/> is generic and C# cannot pass an <em>open</em> generic
    /// as a type argument — <c>SetResponse&lt;SuccessResponse&lt;&gt;&gt;()</c> does not
    /// compile. <c>typeof(SuccessResponse&lt;&gt;)</c> is the idiomatic C# way to pass an open
    /// generic to a method. This is a platform constraint, not a design gap.
    /// </para>
    /// </summary>
    /// <param name="envelopeType">
    /// An open generic type implementing <see cref="ISuccessEnvelope{T}"/>,
    /// <b>or</b> a non‑generic type for no-payload responses.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The type is generic but does not implement <see cref="ISuccessEnvelope{T}"/>,
    /// or is non‑generic and lacks a <c>From(HttpContext, int)</c> method,
    /// or the <c>From</c> method is missing or inaccessible.
    /// </exception>
    public void SetResponse(Type envelopeType)
    {
        // Non-generic type → empty envelope (no payload, e.g. http.Ok() with no args).
        if (!envelopeType.IsGenericTypeDefinition)
        {
            var fromMethod = envelopeType.GetMethod("From", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    $"Success.SetResponse: '{envelopeType.FullName}' must have a " +
                    $"public static 'From(HttpContext httpContext, int statusCode)' method.");

            EmptyFactory = (http, statusCode) =>
                fromMethod.Invoke(null, new object[] { http, statusCode })!;
            return;
        }

        var contract = FindContract(envelopeType)
            ?? throw new InvalidOperationException(
                $"Success.SetResponse requires the envelope to implement " +
                $"ISuccessEnvelope<T>. '{envelopeType.FullName}' does not.");

        var _ = contract.GetMethod("From", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Success.SetResponse: '{envelopeType.FullName}' implements ISuccessEnvelope<T> " +
                $"but its 'From' method is missing.");

        Factory = (data, meta, http, statusCode) =>
        {
            var targetType = envelopeType.IsGenericTypeDefinition
                ? envelopeType.MakeGenericType(data?.GetType() ?? typeof(object))
                : envelopeType;

            var method = FromMethodCache.GetOrAdd(targetType, t =>
                t.GetMethod("From", BindingFlags.Public | BindingFlags.Static)!);

            return method.Invoke(null, new object?[] { data, meta, http, statusCode })!;
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
