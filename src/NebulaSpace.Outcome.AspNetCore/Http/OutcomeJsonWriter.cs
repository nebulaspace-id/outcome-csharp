using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// Writes a response body. When <see cref="OutcomeOptions.JsonSerializerContext"/> is set
/// (NativeAOT), types are resolved through the source-generated context, while the JSON
/// policy — naming, ignore conditions, converters — still comes from the app's
/// <c>HttpJsonOptions</c> (Program.cs). Without a context, falls back to the default
/// <c>WriteAsJsonAsync</c> (JIT).
/// </summary>
internal static class OutcomeJsonWriter
{
    /// <summary>
    /// The reflection-based fallback is guarded, not hidden: it runs only when no
    /// <see cref="OutcomeOptions.JsonSerializerContext"/> is configured — JIT apps, where
    /// trimming does not apply. AOT apps must set a source-generated context; a response
    /// type missing from it fails fast instead of silently reflecting.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Reached only when no JsonSerializerContext is configured (JIT, no trimming). AOT apps must set one.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Reached only when no JsonSerializerContext is configured (JIT). AOT apps must set one.")]
    public static async Task WriteAsync(HttpContext httpContext, object body, OutcomeOptions options, CancellationToken cancellationToken)
    {
        if (options.JsonSerializerContext is { } context)
        {
            // Resolve the app's HttpJsonOptions (Program.cs) so the response keeps the app's
            // policy — camelCase, ignore conditions, converters — while type metadata comes
            // from the source-generated context (AOT-safe, no reflection).
            var appSerializerOptions = httpContext.RequestServices
                .GetService<IOptions<JsonOptions>>()?
                .Value.SerializerOptions;

            if (context.GetTypeInfo(body.GetType()) is null)
            {
                throw new InvalidOperationException(
                    $"The response type '{body.GetType().FullName}' is not in the configured " +
                    "JsonSerializerContext. Add it to your source-generated context so the " +
                    "library can serialize it without reflection.");
            }

            var jsonOptions = appSerializerOptions is not null
                ? new JsonSerializerOptions(appSerializerOptions) { TypeInfoResolver = context }
                : new JsonSerializerOptions { TypeInfoResolver = context };

            await JsonSerializer.SerializeAsync(httpContext.Response.Body, body, jsonOptions, cancellationToken);
            return;
        }

        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken: cancellationToken);
    }
}
