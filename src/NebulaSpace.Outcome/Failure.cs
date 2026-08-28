namespace NebulaSpace.Outcome;

/// <summary>
/// Base class for all failures: the value an <see cref="Outcome{T}"/> carries on its
/// failure branch. A failure holds a human-readable <see cref="Message"/> and an optional
/// machine-readable <see cref="Code"/>.
///
/// <para>
/// Create your own by deriving and passing the message and code to the base constructor.
/// <see cref="Code"/> is what clients switch on — use a stable, snake_case value such as
/// <c>"not_found"</c> or <c>"quota_exceeded"</c>. It is serialized into the error envelope
/// when the failure is mapped to HTTP.
/// </para>
///
/// <para>
/// Failures are immutable: <see cref="Message"/> and <see cref="Code"/> are set once at
/// construction and never change.
/// </para>
///
/// <para>
/// On ASP.NET Core, a failure becomes an HTTP response. Either implement
/// <c>IHttpFailure</c> so the failure knows its own status code, or register a mapper at
/// startup with <c>options.Error.MapFailure&lt;TFailure&gt;(f =&gt; ...)</c>.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public sealed class OrderNotFoundFailure : Failure
/// {
///     public OrderNotFoundFailure(string orderId)
///         : base($"Order '{orderId}' not found.", "order_not_found") { }
/// }
/// </code>
/// </example>
public abstract class Failure
{
    /// <summary>
    /// Human-readable description of what went wrong. Safe to surface to an end user.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Stable, machine-readable code for this failure (e.g. <c>"not_found"</c>).
    /// <c>null</c> when no code was given at construction. Clients can switch on this
    /// value; when the failure maps to HTTP it appears in the error envelope's
    /// <c>code</c> field.
    /// </summary>
    public string? Code { get; }

    /// <summary>Creates a failure without a machine-readable code. Use when the message alone is enough.</summary>
    /// <param name="message">Human-readable description of what went wrong.</param>
    protected Failure(string message)
        : this(message, null)
    {
    }

    /// <summary>
    /// Creates a failure with a message and a stable machine-readable code.
    /// </summary>
    /// <param name="message">Human-readable description of what went wrong.</param>
    /// <param name="code">Stable, machine-readable code (e.g. <c>"not_found"</c>), or <c>null</c>.</param>
    protected Failure(string message, string? code)
    {
        Message = message;
        Code = code;
    }
}
