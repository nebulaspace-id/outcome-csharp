using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
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
///
/// <para>
/// Configure this once inside <c>AddOutcome(options =&gt; ...)</c>. After startup the
/// options are frozen and every mutator throws <see cref="InvalidOperationException"/>.
/// </para>
/// </summary>
public sealed class ErrorOptions
{
    private bool _frozen;
    private bool _includeExceptionDetails;

    // ── Envelope ──────────────────────────────────────────────────

    /// <summary>
    /// Builds a concrete envelope instance from the failure's HTTP intent and the current
    /// request. <c>null</c> until a <c>SetResponse</c> overload is called, after which
    /// <c>ErrorEnvelopeResult</c> uses it instead of <see cref="DefaultErrorResponse"/>.
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
        ThrowIfFrozen();
        Factory = static (details, ctx) => TEnvelope.From(details, ctx);
    }

    /// <summary>
    /// Sets the error envelope by <see cref="Type"/>. Prefer the generic
    /// <see cref="SetResponse{TEnvelope}"/> — it is compile‑time safe and trim‑safe.
    /// This overload exists for scenarios where the envelope type is only known at
    /// runtime. The <c>From</c> method is resolved and compiled to a delegate
    /// <b>once at startup</b>, so requests pay no reflection cost.
    /// </summary>
    /// <param name="envelopeType">
    /// A non‑abstract <see cref="Type"/> implementing <see cref="IErrorEnvelope"/> and
    /// declaring <c>public static IErrorEnvelope From(FailureDetails, HttpContext)</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="envelopeType"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The type is an interface/abstract, does not implement <see cref="IErrorEnvelope"/>,
    /// or has no public static <c>From(FailureDetails, HttpContext)</c> method.
    /// </exception>
    [RequiresUnreferencedCode("Resolves the envelope's From method by reflection. Use SetResponse<TEnvelope>() in trimmed applications.")]
    [RequiresDynamicCode("Compiles the envelope factory with expression trees. Use SetResponse<TEnvelope>() for native AOT.")]
    public void SetResponse(Type envelopeType)
    {
        ThrowIfFrozen();
        if (envelopeType is null) throw new ArgumentNullException(nameof(envelopeType));

        if (!typeof(IErrorEnvelope).IsAssignableFrom(envelopeType) || envelopeType.IsInterface || envelopeType.IsAbstract)
            throw new InvalidOperationException(
                "Error.SetResponse requires a non-abstract class implementing IErrorEnvelope. " +
                $"'{envelopeType.FullName}' does not.");

        var fromMethod = envelopeType.GetMethod(
            nameof(IErrorEnvelope.From),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(FailureDetails), typeof(HttpContext) },
            modifiers: null);

        if (fromMethod is null)
            throw new InvalidOperationException(
                $"'{envelopeType.FullName}' must declare a public static method " +
                "'IErrorEnvelope From(FailureDetails details, HttpContext httpContext)' — " +
                "it implements the static abstract member of IErrorEnvelope.");

        var detailsParameter = Expression.Parameter(typeof(FailureDetails), "details");
        var contextParameter = Expression.Parameter(typeof(HttpContext), "httpContext");
        var call = Expression.Call(fromMethod, detailsParameter, contextParameter);

        Factory = Expression
            .Lambda<Func<FailureDetails, HttpContext, object>>(
                Expression.Convert(call, typeof(object)),
                detailsParameter,
                contextParameter)
            .Compile();
    }

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
    /// <exception cref="ArgumentNullException"><paramref name="mapper"/> is <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// options.Error.MapFailure&lt;QuotaExceededFailure&gt;(f =>
    ///     new FailureDetails(422, f.Message, f.Code));
    /// </code>
    /// </example>
    public void MapFailure<TFailure>(Func<TFailure, FailureDetails> mapper)
        where TFailure : Failure
    {
        ThrowIfFrozen();
        if (mapper is null) throw new ArgumentNullException(nameof(mapper));

        _failureMappers[typeof(TFailure)] = failure => mapper((TFailure)failure);
    }

    /// <summary>
    /// Finds a registered mapper for <paramref name="type"/>, walking its base‑type chain
    /// so a subclass inherits its nearest ancestor's mapping.
    /// </summary>
    internal bool TryGetFailureMapper(Type type, out Func<Failure, FailureDetails> mapper)
    {
        for (var current = type; current is not null && typeof(Failure).IsAssignableFrom(current); current = current.BaseType)
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
    /// <exception cref="ArgumentNullException"><paramref name="mapper"/> is <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// options.Error.MapException&lt;DomainException&gt;(ex =>
    ///     new FailureDetails(422, ex.Message, ex.Code));
    /// </code>
    /// </example>
    public void MapException<TException>(Func<TException, FailureDetails> mapper)
        where TException : Exception
    {
        ThrowIfFrozen();
        if (mapper is null) throw new ArgumentNullException(nameof(mapper));

        _exceptionMappers[typeof(TException)] = ex => mapper((TException)ex);
    }

    /// <summary>
    /// Finds a registered mapper for <paramref name="type"/>, walking its base‑type chain
    /// so a subclass inherits its nearest ancestor's mapping.
    /// </summary>
    internal bool TryGetExceptionMapper(Type type, out Func<Exception, FailureDetails> mapper)
    {
        for (var current = type; current is not null && typeof(Exception).IsAssignableFrom(current); current = current.BaseType)
        {
            if (_exceptionMappers.TryGetValue(current, out mapper!))
                return true;
        }
        mapper = null!;
        return false;
    }

    // ── Framework error defaults ──────────────────────────────────

    private readonly Dictionary<int, StatusCodeDescription> _statusCodes = CreateStatusCodes();

    /// <summary>
    /// Overrides the code and message used when the framework produces
    /// <paramref name="statusCode"/> without running application code — a routing 404, a
    /// method-matching 405, an authentication 401/403, a rate-limiter 429, and so on.
    /// Consumed by <c>UseOutcomeStatusCodePages()</c>.
    ///
    /// <para>
    /// Failures created in application code keep their own message and code; this table
    /// only fills the gap for responses the framework writes with an empty body.
    /// </para>
    /// </summary>
    /// <param name="statusCode">The HTTP status code. Must be an error status (400–599).</param>
    /// <param name="code">Machine-readable code, e.g. <c>"not_found"</c>.</param>
    /// <param name="message">Human-readable message rendered into the envelope.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="statusCode"/> is outside 400–599.</exception>
    /// <exception cref="ArgumentException"><paramref name="code"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// options.Error.MapStatusCode(418, "teapot", "I am a teapot.");
    /// </code>
    /// </example>
    public void MapStatusCode(int statusCode, string code, string message)
    {
        ThrowIfFrozen();
        if (statusCode is < 400 or > 599)
            throw new ArgumentOutOfRangeException(
                nameof(statusCode), statusCode, "Only error status codes (400-599) can be mapped.");
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("A machine-readable code is required.", nameof(code));
        if (message is null) throw new ArgumentNullException(nameof(message));

        _statusCodes[statusCode] = new StatusCodeDescription(statusCode, code, message);
    }

    /// <summary>
    /// Returns the configured description for <paramref name="statusCode"/>. Unmapped codes
    /// fall back to <c>http_{status}</c> plus the framework reason phrase.
    /// </summary>
    internal StatusCodeDescription GetStatusCodeDescription(int statusCode) =>
        _statusCodes.TryGetValue(statusCode, out var description)
            ? description
            : new StatusCodeDescription(
                statusCode,
                $"http_{statusCode}",
                ReasonPhrases.GetReasonPhrase(statusCode) is { Length: > 0 } reason
                    ? reason
                    : "The request failed.");

    private static Dictionary<int, StatusCodeDescription> CreateStatusCodes() => new()
    {
        [StatusCodes.Status400BadRequest] = new(400, "bad_request", "The request is invalid."),
        [StatusCodes.Status401Unauthorized] = new(401, "unauthorized", "Authentication is required."),
        [StatusCodes.Status403Forbidden] = new(403, "forbidden", "You do not have permission to access this resource."),
        [StatusCodes.Status404NotFound] = new(404, "not_found", "The requested resource was not found."),
        [StatusCodes.Status405MethodNotAllowed] = new(405, "method_not_allowed", "The HTTP method is not allowed for this resource."),
        [StatusCodes.Status406NotAcceptable] = new(406, "not_acceptable", "The requested representation is not available."),
        [StatusCodes.Status408RequestTimeout] = new(408, "request_timeout", "The request timed out."),
        [StatusCodes.Status409Conflict] = new(409, "conflict", "The request conflicts with the current state of the resource."),
        [StatusCodes.Status412PreconditionFailed] = new(412, "precondition_failed", "A precondition for this request failed."),
        [StatusCodes.Status413PayloadTooLarge] = new(413, "payload_too_large", "The request payload is too large."),
        [StatusCodes.Status415UnsupportedMediaType] = new(415, "unsupported_media_type", "The request content type is not supported."),
        [StatusCodes.Status422UnprocessableEntity] = new(422, "unprocessable_entity", "The request could not be processed."),
        [StatusCodes.Status429TooManyRequests] = new(429, "too_many_requests", "Too many requests. Try again later."),
        [StatusCodes.Status500InternalServerError] = new(500, "internal_error", "An error occurred while processing your request."),
        [StatusCodes.Status501NotImplemented] = new(501, "not_implemented", "The requested operation is not implemented."),
        [StatusCodes.Status502BadGateway] = new(502, "bad_gateway", "The upstream service returned an invalid response."),
        [StatusCodes.Status503ServiceUnavailable] = new(503, "service_unavailable", "The service is temporarily unavailable."),
        [StatusCodes.Status504GatewayTimeout] = new(504, "gateway_timeout", "The upstream service did not respond in time."),
    };

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
    public bool IncludeExceptionDetails
    {
        get => _includeExceptionDetails;
        set
        {
            ThrowIfFrozen();
            _includeExceptionDetails = value;
        }
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
