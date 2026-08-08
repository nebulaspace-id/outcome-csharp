using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Options for the Outcome ASP.NET Core integration, configured at startup via
/// <c>AddOutcome</c>. One object configures successes, errors, failure/exception
/// mappings, and exception-handling behavior — no separate registrations.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddOutcome(options =>
/// {
///     options.Success.SetResponse&lt;SuccessResponse&gt;();
///     options.Error.SetResponse&lt;ErrorResponse&gt;();
///     options.Error.MapFailure&lt;QuotaExceededFailure&gt;(f =>
///         new FailureDetails(422, f.Message, f.Code));
///     options.Error.MapException&lt;DomainException&gt;(ex =>
///         new FailureDetails(422, ex.Message, ex.Code));
///     options.Error.IncludeExceptionDetails = env.IsDevelopment();
/// });
/// </code>
/// </example>
public sealed class OutcomeOptions
{
    /// <summary>
    /// Configuration for the success envelope — whether successful responses are wrapped
    /// and in which type. Disable via <c>options.Success.Enabled = false</c>.
    /// </summary>
    public SuccessOptions Success { get; } = new();

    /// <summary>
    /// Configuration for the error envelope, failure mappers, exception mappers, and
    /// unhandled-exception behavior — all in one place. Access via <c>options.Error</c>.
    /// </summary>
    public ErrorOptions Error { get; } = new();

    // ── Internal facades for the rest of the library ──────────────────

    internal Func<object?, PageInfo?, HttpContext, int, object>? SuccessFactory =>
        Success.Factory;

    internal Func<FailureDetails, HttpContext, object>? ErrorFactory =>
        Error.Factory;

    internal bool TryGetFailureMapper(Type type, out Func<Failure, FailureDetails> mapper) =>
        Error.TryGetFailureMapper(type, out mapper);

    internal bool TryGetExceptionMapper(Type type, out Func<Exception, FailureDetails> mapper) =>
        Error.TryGetExceptionMapper(type, out mapper);
}
