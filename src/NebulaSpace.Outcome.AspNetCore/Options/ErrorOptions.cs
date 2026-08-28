using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Configuration for the error envelope, failure-to-HTTP mappers, exception-to-HTTP
/// mappers, and unhandled-exception behavior — all in one place. Access via
/// <c>options.Error</c> from <see cref="OutcomeOptions"/>.
///
/// <para>
/// By default the error envelope is clean RFC 7807 <see cref="DefaultErrorResponse"/>
/// (title/status/detail only). Use <see cref="SetResponse{TEnvelope}"/> to make it your own.
/// </para>
///
/// <para>
/// Three layers of error handling, consulted in order:
/// <list type="number">
/// <item><see cref="MapFailure{TFailure}"/> — explicit startup‑registered mapping wins above all.</item>
/// <item><see cref="IHttpFailure"/> — the failure maps itself (Open/Closed).</item>
/// <item>Unknown failures degrade to 500 Internal Server Error (never throws).</item>
/// </list>
/// </para>
/// </summary>
public sealed class ErrorOptions
{
    // ── Envelope ──────────────────────────────────────────────────

    /// <summary>
    /// Builds a concrete envelope instance from the failure's HTTP intent and the current
    /// request. <c>null</c> until a <c>SetResponse</c> overload is called, after which
    /// <see cref="ErrorEnvelopeResult"/> uses it instead of <see cref="DefaultErrorResponse"/>.
    /// </summary>
    internal Func<FailureDetails, HttpContext, object>? Factory { get; private set; }

    /// <summary>
    /// Sets the error envelope to a type implementing <see cref="IErrorEnvelope"/>.
    /// Dispatches through the static abstract <c>From(FailureDetails, HttpContext)</c>
    /// directly — no <c>GetMethod</c>, no <c>Invoke</c>, no allocations beyond the
    /// envelope itself.
    ///
    /// <para>
    /// <b>Why is this generic while Success uses <c>typeof()</c>?</b>
    /// <see cref="IErrorEnvelope"/> is non‑generic — there is one envelope type for the
    /// whole application — so <c>SetResponse&lt;T&gt;()</c> works at compile time.
    /// <see cref="ISuccessEnvelope{T}"/> is generic (one envelope per payload type),
    /// and C# cannot pass an <em>open</em> generic as a type argument —
    /// <c>SetResponse&lt;SomeEnvelope&lt;&gt;&gt;()</c> does not compile — so Success
    /// uses <c>SuccessOptions.SetResponse(Type)</c> with <c>typeof(Envelope&lt;&gt;)</c>
    /// instead. This is a C# language constraint, not a design gap.
    /// </para>
    /// </summary>
    /// <typeparam name="TEnvelope">A type implementing <see cref="IErrorEnvelope"/>.</typeparam>
    /// <example>
    /// <code>
    /// options.Error.SetResponse&lt;ErrorResponse&gt;();
    /// </code>
    /// </example>
    public void SetResponse<TEnvelope>() where TEnvelope : IErrorEnvelope
    {
        Factory = (details, ctx) => TEnvelope.From(details, ctx);
    }

    /// <summary>
    /// Sets the error envelope by <see cref="Type"/>. Prefer the generic
    /// <see cref="SetResponse{TEnvelope}"/> — it is compile‑time safe.
    /// This overload exists for scenarios where the envelope type is only known at
    /// runtime; it caches the <see cref="MethodInfo"/> per type so the reflection cost
    /// is paid once.
    /// </summary>
    /// <param name="envelopeType">
    /// A <see cref="Type"/> implementing <see cref="IErrorEnvelope"/>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="envelopeType"/> does not implement <see cref="IErrorEnvelope"/>.
    /// </exception>
    public void SetResponse(Type envelopeType)
    {
        if (!typeof(IErrorEnvelope).IsAssignableFrom(envelopeType))
            throw new InvalidOperationException(
                $"Error.SetResponse requires the envelope to implement IErrorEnvelope. " +
                $"'{envelopeType.FullName}' does not.");

        var fromMethod = DelegateCache.GetOrAdd(envelopeType, t =>
            t.GetMethod("From", BindingFlags.Public | BindingFlags.Static)!);

        Factory = (details, ctx) => fromMethod.Invoke(null, new object[] { details, ctx })!;
    }

    /// <summary>
    /// Per‑type <see cref="MethodInfo"/> cache — used only by
    /// <see cref="SetResponse(Type)"/>. Each entry is created once and reused for the
    /// lifetime of the process. The dictionary stays small — one entry per distinct
    /// error‑envelope type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, MethodInfo> DelegateCache = new();

    // ── Failure mapping ───────────────────────────────────────────

    private readonly Dictionary<Type, Func<Failure, FailureDetails>> _failureMappers = new();

    /// <summary>
    /// Maps a failure type to HTTP intent. The mapper returns <see cref="FailureDetails"/>
    /// (status + message + code + errors), which the configured error envelope renders.
    /// A subclass of a registered failure inherits its nearest ancestor's mapping.
    ///
    /// <para>
    /// Registered mappers are consulted <b>BEFORE</b> the failure's own
    /// <see cref="IHttpFailure.ToDetails"/> implementation, so an explicit
    /// registration can override built‑in failures.
    /// </para>
    /// </summary>
    /// <typeparam name="TFailure">The failure type to map. Must inherit from <see cref="Failure"/>.</typeparam>
    /// <param name="mapper">A function that returns <see cref="FailureDetails"/> from the failure instance.</param>
    /// <example>
    /// <code>
    /// options.Error.MapFailure&lt;QuotaExceededFailure&gt;(f =>
    ///     new FailureDetails(422, f.Message, f.Code));
    /// </code>
    /// </example>
    public void MapFailure<TFailure>(Func<TFailure, FailureDetails> mapper)
        where TFailure : Failure
        => _failureMappers[typeof(TFailure)] = failure => mapper((TFailure)failure);

    /// <summary>
    /// Finds a registered mapper for <paramref name="type"/>, walking its base‑type chain
    /// so a subclass inherits its nearest ancestor's mapping.
    /// </summary>
    internal bool TryGetFailureMapper(Type type, out Func<Failure, FailureDetails> mapper)
    {
        for (var current = type; current is not null && current != typeof(Failure); current = current.BaseType!)
        {
            if (_failureMappers.TryGetValue(current, out mapper!))
                return true;
        }
        mapper = null!;
        return false;
    }

    // ── Exception mapping ─────────────────────────────────────────

    private readonly Dictionary<Type, Func<Exception, FailureDetails>> _exceptionMappers = new();

    /// <summary>
    /// Maps an exception type to HTTP intent. The mapper returns <see cref="FailureDetails"/>
    /// (status + message + code + errors), which the configured error envelope renders.
    /// A subclass of a registered exception inherits its nearest ancestor's mapping.
    ///
    /// <para>
    /// Mapped exceptions bypass <see cref="IncludeExceptionDetails"/> gating — the mapper's
    /// output is authoritative and is returned verbatim regardless of that setting.
    /// </para>
    /// </summary>
    /// <typeparam name="TException">The exception type to map. Must inherit from <see cref="Exception"/>.</typeparam>
    /// <param name="mapper">
    /// A function that returns <see cref="FailureDetails"/> from the exception instance.
    /// This becomes the response intent; the configured error envelope renders it into a body.
    /// </param>
    /// <example>
    /// <code>
    /// options.Error.MapException&lt;DomainException&gt;(ex =>
    ///     new FailureDetails(422, ex.Message, ex.Code));
    /// </code>
    /// </example>
    public void MapException<TException>(Func<TException, FailureDetails> mapper)
        where TException : Exception
        => _exceptionMappers[typeof(TException)] = ex => mapper((TException)ex);

    /// <summary>
    /// Finds a registered mapper for <paramref name="type"/>, walking its base‑type chain
    /// so a subclass inherits its nearest ancestor's mapping.
    /// </summary>
    internal bool TryGetExceptionMapper(Type type, out Func<Exception, FailureDetails> mapper)
    {
        for (var current = type; current is not null && current != typeof(Exception); current = current.BaseType!)
        {
            if (_exceptionMappers.TryGetValue(current, out mapper!))
                return true;
        }
        mapper = null!;
        return false;
    }

    // ── Exception handling ────────────────────────────────────────

    /// <summary>
    /// Whether the exception message is included in the error envelope's <c>detail</c>
    /// for <b>UNHANDLED</b> exceptions (the generic 500 fallback). Default <c>false</c> —
    /// a generic message is returned; the full exception is logged server‑side.
    /// Typically enabled in Development only.
    ///
    /// <para>
    /// <b>Does not apply to mapped exceptions.</b> Exceptions registered via
    /// <see cref="MapException{TException}"/> bypass this gating — the mapper's output
    /// is returned as‑is regardless of this setting.
    /// </para>
    /// </summary>
    public bool IncludeExceptionDetails { get; set; }
}
