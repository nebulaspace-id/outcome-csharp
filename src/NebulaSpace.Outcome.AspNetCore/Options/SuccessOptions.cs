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
/// <para>
/// Two ways to configure the envelope:
/// <list type="number">
/// <item>
/// <see cref="SetResponse(Func{object?, IPaginationDetails?, HttpContext, int, object})"/> —
///   delegate overload, stores the factory as‑is.
/// </item>
/// <item>
/// <see cref="SetResponse(Type)"/> — open‑generic path for typed Swagger. Uses
///   <c>MakeGenericType</c> + cached <c>MethodInfo.Invoke</c> at runtime.
/// </item>
/// </list>
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
    /// Builds the concrete envelope instance from the payload, optional pagination, the
    /// current request, and the HTTP status. <c>null</c> until a <c>SetResponse</c>
    /// overload is called; when <c>null</c> the library falls back to
    /// <see cref="DefaultSuccessResponse{T}"/>.
    /// </summary>
    internal Func<object?, IPaginationDetails?, HttpContext, int, object>? Factory { get; private set; }

    /// <summary>
    /// Builds the concrete envelope instance from the payload, its concrete pagination
    /// type, the current request, and the HTTP status. Only set when
    /// <see cref="SetResponse(Type)"/> receives a <b>2‑param</b> open generic (e.g.
    /// <c>typeof(SuccessResponse&lt;,&gt;)</c>). When non‑null and the response carries
    /// pagination, the library prefers this factory over <see cref="Factory"/> so
    /// System.Text.Json and Swagger see the full concrete pagination shape
    /// (<see cref="OffsetInfo"/> or <see cref="CursorInfo"/>).
    /// </summary>
    internal Func<object?, IPaginationDetails?, HttpContext, int, object>? PaginationFactory { get; private set; }

    /// <summary>
    /// Stores the factory delegate as‑is — every request is a plain delegate call, zero
    /// reflection. Useful when the envelope shape is built inline or the constructor needs
    /// extra wiring.
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
    public void SetResponse(Func<object?, IPaginationDetails?, HttpContext, int, object> factory)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    // ── Open-generic path (Swagger fidelity) ───────────────────────────

    /// <summary>
    /// Sets the success envelope to an <b>open generic</b> type. When the type has
    /// <b>one</b> type parameter (e.g. <c>typeof(ApiSuccess&lt;&gt;)</c>), the library
    /// closes it with the payload type per request so Swagger sees
    /// <c>Data: Order</c> rather than <c>Data: object</c>.
    ///
    /// <para>
    /// When the type has <b>two</b> type parameters (e.g.
    /// <c>typeof(SuccessResponse&lt;,&gt;)</c>), the library closes the second parameter
    /// with the concrete pagination type — <see cref="OffsetInfo"/> or
    /// <see cref="CursorInfo"/> — so System.Text.Json and Swagger see the
    /// full pagination shape, not just the <see cref="IPaginationDetails"/> interface members.
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
    ///
    /// <para>
    /// <b>Uses <c>MakeGenericType</c> + cached <c>MethodInfo.Invoke</c>.</b>
    /// Not AOT‑safe; for AOT scenarios use the delegate overload
    /// <see cref="SetResponse(Func{object?, IPaginationDetails?, HttpContext, int, object})"/>.
    /// </para>
    /// </summary>
    /// <param name="envelopeType">
    /// An open generic type implementing <see cref="ISuccessEnvelope{T}"/>.
    /// 1‑param for non‑paginated (<c>typeof(ApiSuccess&lt;&gt;)</c>),
    /// 2‑param for paginated (<c>typeof(SuccessResponse&lt;,&gt;)</c>).
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The type does not implement <see cref="ISuccessEnvelope{T}"/>, or its
    /// <c>From</c> method is missing or inaccessible.
    /// </exception>
    /// <example>
    /// <code>
    /// // 1‑param open generic: T is the payload type, closed per request
    /// options.Success.SetResponse(typeof(ApiSuccess&lt;&gt;));
    ///
    /// // 2‑param open generic: TData + TPage, closed per request
    /// // Pagination is typed as OffsetInfo or CursorInfo — full shape in STJ/Swagger
    /// options.Success.SetResponse(typeof(SuccessResponse&lt;,&gt;));
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

        // When the envelope has two type parameters (e.g. SuccessResponse<TData, TPage>),
        // route to PaginationFactory — it closes both TData (from the payload) and TPage
        // (from the concrete pagination type) so STJ and Swagger see the full shape.
        var isTwoParam = envelopeType.IsGenericTypeDefinition
                         && envelopeType.GetGenericArguments().Length == 2;

        if (isTwoParam)
        {
            PaginationFactory = (data, pagination, http, statusCode) =>
            {
                var paginationType = pagination?.GetType() ?? typeof(object);
                var dataType = data?.GetType() ?? typeof(object);
                var targetType = envelopeType.MakeGenericType(dataType, paginationType);

                var method = FromMethodCache.GetOrAdd(targetType, t =>
                    t.GetMethod("From", BindingFlags.Public | BindingFlags.Static)!);

                return method.Invoke(null, new object?[] { data, pagination, http, statusCode })!;
            };

            return;
        }

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
