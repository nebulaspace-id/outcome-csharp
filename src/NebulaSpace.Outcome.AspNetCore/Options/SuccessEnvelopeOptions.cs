using System.Reflection;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Configuration for the success envelope: whether successful responses are wrapped, and
/// — if so — in which type. When no envelope is set, successful responses are wrapped in
/// <c>DefaultSuccessResponse&lt;T&gt;</c> (or returned raw when <see cref="Enabled"/> is
/// <c>false</c>). Error responses always use an envelope regardless of this setting.
/// </summary>
public sealed class SuccessEnvelopeOptions
{
    /// <summary>
    /// Whether successful responses are wrapped in a success envelope. Default <c>true</c>.
    /// When <c>false</c>, <c>Ok</c>/<c>Created</c> return the raw payload.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Builds the concrete envelope instance from the payload, optional pagination, the
    /// current request, and the HTTP status the response will use (<c>200</c> for
    /// <c>Ok</c>, <c>201</c> for <c>Created</c>). <c>null</c> until <see cref="Set(Type)"/>
    /// is called.
    /// </summary>
    internal Func<object?, PageInfo?, HttpContext, int, object>? Factory { get; private set; }

    /// <summary>
    /// Sets the success envelope to a custom type. Pass an <b>open generic</b> type whose
    /// type parameter is the payload type — the library closes it per request:
    /// <code>
    /// options.SuccessEnvelope.Set(typeof(ApiSuccess&lt;&gt;));
    /// </code>
    /// The envelope type must implement <see cref="ISuccessEnvelope{T}"/>, whose
    /// <c>From</c> factory builds it from the payload, optional pagination, the current
    /// request, and the response's status:
    /// <code>
    /// public sealed class ApiSuccess&lt;T&gt; : ISuccessEnvelope&lt;T&gt;
    /// {
    ///     public static ISuccessEnvelope&lt;T&gt; From(T data, PageInfo? pagination, HttpContext http, int statusCode) =&gt; new()
    ///     {
    ///         TraceId = http.TraceIdentifier, StatusCode = statusCode, Data = data, Pagination = pagination,
    ///     };
    /// }
    /// </code>
    /// The returned instance is serialized as-is, so OpenAPI
    /// (<c>.Produces&lt;ApiSuccess&lt;T&gt;&gt;()</c>) documents the exact success shape,
    /// with the payload's type substituted for <c>T</c>. A type that does not implement
    /// <see cref="ISuccessEnvelope{T}"/> throws <see cref="InvalidOperationException"/> here,
    /// at startup — not at request time.
    /// </summary>
    public void Set(Type envelopeType)
    {
        var contract = FindContract(envelopeType)
            ?? throw new InvalidOperationException(
                $"SuccessEnvelope.Set requires the envelope to implement " +
                $"ISuccessEnvelope<T>, whose 'From(T data, PageInfo? pagination, " +
                $"HttpContext http, int statusCode)' builds the response body. " +
                $"'{envelopeType.FullName}' does not implement ISuccessEnvelope<T>.");

        var from = contract.GetMethod(
            "From", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"SuccessEnvelope.Set: '{envelopeType.FullName}' implements ISuccessEnvelope<T> " +
                $"but its 'From' method is missing or inaccessible.");

        Factory = (data, pagination, http, statusCode) =>
        {
            // Open generic envelope (typeof(ApiSuccess<>)): close its type parameter with
            // the payload's RUNTIME type so the serialized data keeps its concrete shape.
            var targetType = envelopeType.IsGenericTypeDefinition
                ? envelopeType.MakeGenericType(data?.GetType() ?? typeof(object))
                : envelopeType;

            var factory = targetType.GetMethod("From", BindingFlags.Public | BindingFlags.Static)!;
            return factory.Invoke(null, new object?[] { data, pagination, http, statusCode })!;
        };
    }

    /// <summary>Finds the <c>ISuccessEnvelope&lt;T&gt;</c> implementation on the type (or its open generic form).</summary>
    private static Type? FindContract(Type envelopeType)
    {
        if (envelopeType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISuccessEnvelope<>)))
        {
            return envelopeType;
        }

        if (envelopeType.IsGenericTypeDefinition)
        {
            foreach (var candidate in envelopeType.GetInterfaces())
            {
                if (candidate.IsGenericTypeDefinition &&
                    candidate.GetGenericTypeDefinition() == typeof(ISuccessEnvelope<>))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
