using Microsoft.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// The library's default envelope for a successful operation. Carries the payload,
/// the status code, and — when the app attached one — response <b>metadata</b>.
///
/// <para>
/// <see cref="Meta"/> is <see cref="object"/> because the metadata is the app's
/// business: the library never inspects it, and System.Text.Json serializes it by
/// its runtime type, so every property the app's type declares appears in the body.
/// A paginated response simply passes a meta object whose <c>pagination</c> property
/// holds the page information.
/// </para>
///
/// <para>
/// Disable via <c>options.Success.Enabled = false</c> to return the raw payload
/// instead.  To use a different shape, implement <see cref="ISuccessEnvelope{T}"/>
/// and register it with <c>options.Success.SetResponse(typeof(MySuccess&lt;&gt;))</c>.
/// </para>
/// </summary>
/// <typeparam name="T">The payload type.</typeparam>
public sealed class DefaultSuccessResponse<T> : ISuccessEnvelope<T>
{
    /// <summary>The HTTP status code for this response.</summary>
    public int StatusCode { get; set; }

    /// <summary>The payload.</summary>
    public T? Data { get; set; }

    /// <summary>
    /// Response metadata the app attached to this response, or <c>null</c> when the
    /// response carries none. Serialized by its runtime type.
    /// </summary>
    public object? Meta { get; set; }

    /// <inheritdoc />
    public static ISuccessEnvelope<T> From(T data, object? meta, HttpContext httpContext, int statusCode) =>
        new DefaultSuccessResponse<T>
        {
            StatusCode = statusCode,
            Data = data,
            Meta = meta,
        };
}
