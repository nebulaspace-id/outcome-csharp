using System.Text.Json;
using System.Text.Json.Serialization;
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

    /// <summary>
    /// Pagination caps (<see cref="PaginationOptions.MaxPage"/>, <see cref="PaginationOptions.MaxLimit"/>)
    /// copied to the static defaults on <see cref="Pagination.DefaultOffsetQuery"/> and
    /// <see cref="Pagination.DefaultCursorQuery"/> at startup. Configurable via
    /// <c>options.Pagination.MaxPage/MaxLimit</c> in <c>AddOutcome</c>.
    /// </summary>
    public PaginationOptions Pagination { get; } = new();

    /// <summary>
    /// A source-generated <see cref="JsonSerializerContext"/> covering every type the library
    /// writes to the response body — the success and error envelopes, your payload types, and
    /// your pagination metadata types. <b>Required for NativeAOT</b>: when set, the library
    /// serializes envelopes through it (via <c>GetTypeInfo</c>), so no reflection is used.
    ///
    /// <para>
    /// When <c>null</c> (the default), the library uses <c>WriteAsJsonAsync</c> with your
    /// app's JSON options — fine for JIT apps, but not NativeAOT-safe.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [JsonSerializable(typeof(SuccessResponse))]
    /// [JsonSerializable(typeof(ErrorResponse))]
    /// public partial class AppJsonContext : JsonSerializerContext { }
    ///
    /// options.JsonSerializerContext = AppJsonContext.Default;
    /// </code>
    /// </example>
    public JsonSerializerContext? JsonSerializerContext { get; set; }

    // ── Internal facades for the rest of the library ──────────────────

    internal Func<object?, IPaginationDetails?, HttpContext, int, object>? SuccessFactory =>
        Success.Factory;

    internal Func<object?, IPaginationDetails?, HttpContext, int, object>? SuccessPaginationFactory =>
        Success.PaginationFactory;

    internal Func<FailureDetails, HttpContext, object>? ErrorFactory =>
        Error.Factory;

    internal bool TryGetFailureMapper(Type type, out Func<Failure, FailureDetails> mapper) =>
        Error.TryGetFailureMapper(type, out mapper);

    internal bool TryGetExceptionMapper(Type type, out Func<Exception, FailureDetails> mapper) =>
        Error.TryGetExceptionMapper(type, out mapper);
}
