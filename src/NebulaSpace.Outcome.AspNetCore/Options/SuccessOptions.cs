using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Configuration for the success envelope. Access via <c>options.Success</c> from
/// <see cref="OutcomeOptions"/>.
///
/// <para>
/// By default the success envelope is <see cref="DefaultSuccessResponse{T}"/>:
/// <c>{ statusCode, data, meta? }</c>. Use
/// <see cref="SetResponse(Type)"/> or <see cref="SetResponse(Func{object?, object?, HttpContext, int, object})"/> to make it
/// your own.
/// </para>
///
/// <para>
/// Configure this once inside <c>AddOutcome(options =&gt; ...)</c>. After startup the
/// options are frozen and every mutator throws <see cref="InvalidOperationException"/>.
/// </para>
/// </summary>
public sealed class SuccessOptions
{
    private bool _frozen;
    private bool _enabled = true;

    // ── Envelope ──────────────────────────────────────────────────

    /// <summary>
    /// Builds a concrete envelope instance for one success response. <c>null</c> until a
    /// <c>SetResponse</c> overload is called, after which success results use it instead
    /// of <see cref="DefaultSuccessResponse{T}"/>.
    /// </summary>
    internal Func<object?, object?, HttpContext, int, object>? Factory { get; private set; }

    /// <summary>
    /// Whether success responses are wrapped in the configured envelope. Default
    /// <c>true</c>. Set to <c>false</c> to return the payload raw (e.g. an API that must
    /// not have a success envelope).
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            ThrowIfFrozen();
            _enabled = value;
        }
    }

    /// <summary>
    /// Sets the success envelope to an <b>open generic</b> type implementing
    /// <see cref="ISuccessEnvelope{T}"/>. The library closes <c>T</c> with the payload's
    /// runtime type per request, so Swagger documents <c>Data: Order</c> rather than
    /// <c>Data: object</c>.
    ///
    /// <para>
    /// This overload takes <see cref="Type"/> (not a generic parameter) because C# cannot
    /// pass an <em>open</em> generic as a type argument:
    /// <c>SetResponse&lt;MyEnvelope&lt;&gt;&gt;()</c> does not compile. Use the
    /// <see cref="SetResponse(Func{object?, object?, HttpContext, int, object})"/>
    /// delegate overload for AOT/trim‑safe scenarios.
    /// </para>
    /// </summary>
    /// <param name="envelopeType">
    /// An open generic type definition (e.g. <c>typeof(SuccessResponse&lt;&gt;)</c>) whose
    /// closed form implements <see cref="ISuccessEnvelope{T}"/> and declares
    /// <c>public static ISuccessEnvelope&lt;T&gt; From(T, object?, HttpContext, int)</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="envelopeType"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The type is not an open generic, does not implement <see cref="ISuccessEnvelope{T}"/>,
    /// or its <c>From</c> method is missing or inaccessible.
    /// </exception>
    [RequiresUnreferencedCode("Resolves the envelope's From method by reflection. Use the delegate overload in trimmed applications.")]
    [RequiresDynamicCode("Compiles the envelope factory with expression trees. Use the delegate overload for native AOT.")]
    public void SetResponse(Type envelopeType)
    {
        ThrowIfFrozen();
        if (envelopeType is null) throw new ArgumentNullException(nameof(envelopeType));

        if (!envelopeType.IsGenericTypeDefinition)
            throw new InvalidOperationException(
                $"Success.SetResponse requires an open generic type definition implementing " +
                $"ISuccessEnvelope<T> (for example typeof(SuccessResponse<>)), but '{envelopeType.FullName}' " +
                "is not an open generic.");

        if (!ImplementsEnvelopeContract(envelopeType))
            throw new InvalidOperationException(
                $"'{envelopeType.FullName}' must implement ISuccessEnvelope<T> to be used as a success envelope.");

        // Validate the From signature now, when a payload type can be closed.
        ValidateFromMethod(envelopeType);

        var invokers = new ConcurrentDictionary<Type, Func<object?, object?, HttpContext, int, object>>();

        Factory = (data, meta, httpContext, statusCode) =>
        {
            var payloadType = data?.GetType() ?? typeof(object);
            var invoker = invokers.GetOrAdd(payloadType, type => BuildInvoker(envelopeType, type));
            return invoker(data, meta, httpContext, statusCode);
        };
    }

    /// <summary>
    /// Sets the success envelope with a delegate — no reflection, no expression trees,
    /// trim‑ and AOT‑safe. The delegate receives the payload, meta, request, and status,
    /// and returns the envelope instance to serialize.
    /// </summary>
    /// <param name="factory">Builds the envelope body for one response.</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// options.Success.SetResponse((data, meta, ctx, status) =>
    ///     new SuccessResponse&lt;object&gt; { StatusCode = status, Data = data });
    /// </code>
    /// </example>
    public void SetResponse(Func<object?, object?, HttpContext, int, object> factory)
    {
        ThrowIfFrozen();
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    private static bool ImplementsEnvelopeContract(Type envelopeType)
    {
        foreach (var candidate in envelopeType.GetInterfaces())
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(ISuccessEnvelope<>))
                return true;
        }

        return false;
    }

    private static void ValidateFromMethod(Type envelopeType)
    {
        Type closedType;
        try
        {
            closedType = envelopeType.MakeGenericType(typeof(object));
        }
        catch (ArgumentException)
        {
            // The envelope constrains T (e.g. `where T : struct`), so object cannot close
            // it. Validation happens on the first real payload; the error message there
            // is equally clear.
            return;
        }

        if (FindFromMethod(closedType, typeof(object)) is null)
        {
            throw new InvalidOperationException(
                $"'{envelopeType.FullName}' must declare a public static method " +
                "'ISuccessEnvelope<T> From(T data, object? meta, HttpContext httpContext, int statusCode)' — " +
                "it implements the static abstract member of ISuccessEnvelope<T>.");
        }
    }

    private static MethodInfo? FindFromMethod(Type closedEnvelopeType, Type payloadType) =>
        closedEnvelopeType.GetMethod(
            "From",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { payloadType, typeof(object), typeof(HttpContext), typeof(int) },
            modifiers: null);

    private static Func<object?, object?, HttpContext, int, object> BuildInvoker(Type envelopeType, Type payloadType)
    {
        Type closedEnvelopeType;
        try
        {
            closedEnvelopeType = envelopeType.MakeGenericType(payloadType);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                $"Success envelope '{envelopeType.FullName}' cannot be closed with payload type " +
                $"'{payloadType.FullName}'. Check its generic constraints.", ex);
        }

        var fromMethod = FindFromMethod(closedEnvelopeType, payloadType)
            ?? throw new InvalidOperationException(
                $"Success envelope '{closedEnvelopeType.FullName}' must declare a public static method " +
                "'ISuccessEnvelope<T> From(T data, object? meta, HttpContext httpContext, int statusCode)'.");

        var dataParameter = Expression.Parameter(typeof(object), "data");
        var metaParameter = Expression.Parameter(typeof(object), "meta");
        var httpContextParameter = Expression.Parameter(typeof(HttpContext), "httpContext");
        var statusCodeParameter = Expression.Parameter(typeof(int), "statusCode");

        var call = Expression.Call(
            fromMethod,
            Expression.Convert(dataParameter, payloadType),
            metaParameter,
            httpContextParameter,
            statusCodeParameter);

        return Expression
            .Lambda<Func<object?, object?, HttpContext, int, object>>(
                Expression.Convert(call, typeof(object)),
                dataParameter,
                metaParameter,
                httpContextParameter,
                statusCodeParameter)
            .Compile();
    }

    /// <summary>Freezes the configuration. Called once by <c>AddOutcome</c> after the user delegate runs.</summary>
    internal void Freeze() => _frozen = true;

    private void ThrowIfFrozen()
    {
        if (_frozen)
            throw new InvalidOperationException(
                "OutcomeOptions have already been initialized. Configure them once inside AddOutcome(options => ...); " +
                "mutating them after the service provider is built is not supported.");
    }
}
