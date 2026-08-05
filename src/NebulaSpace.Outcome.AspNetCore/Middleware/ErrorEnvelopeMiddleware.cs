using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Middleware;

/// <summary>
/// Injects configured properties into error envelope responses (HTTP status &gt;= 400).
/// By default, this adds a <c>traceId</c> property from <see cref="HttpContext.TraceIdentifier"/>.
/// Success responses and non-envelope bodies are passed through untouched.
/// </summary>
public sealed class ErrorEnvelopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ResultPatternOptions _options;

    public ErrorEnvelopeMiddleware(RequestDelegate next, ResultPatternOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            if (_options.ErrorEnvelopeProperties.Count > 0 &&
                context.Response.StatusCode >= 400 &&
                buffer.Length > 0)
            {
                await InjectPropertiesAsync(context, buffer, originalBody);
            }
            else
            {
                buffer.Position = 0;
                await buffer.CopyToAsync(originalBody);
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private async Task InjectPropertiesAsync(HttpContext context, MemoryStream buffer, Stream originalBody)
    {
        var contentType = context.Response.ContentType;
        if (contentType is null ||
            !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
            return;
        }

        buffer.Position = 0;
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(buffer);
        }
        catch (JsonException)
        {
            node = null;
        }

        if (node is not JsonObject json)
        {
            // Body is not a JSON object — pass through as-is.
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
            return;
        }

        // Only inject into bodies that are one of our error envelopes.
        if (json.TryGetPropertyValue("statusCode", out var status) &&
            status is JsonValue statusValue &&
            statusValue.GetValue<int>() >= 400)
        {
            foreach (var (propertyName, valueFactory) in _options.ErrorEnvelopeProperties)
            {
                var value = valueFactory(context);
                json[propertyName] = value is null ? null : JsonSerializer.SerializeToNode(value, value.GetType());
            }
        }

        // Re-serialize the (possibly modified) envelope back into the buffer.
        buffer.SetLength(0);
        buffer.Position = 0;
        using (var writer = new Utf8JsonWriter(buffer))
        {
            json.WriteTo(writer);
        }

        context.Response.ContentLength = buffer.Length;
        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody);
    }
}

public static class ErrorEnvelopeMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorEnvelopeMiddleware(this IApplicationBuilder app) =>
        app.UseMiddleware<ErrorEnvelopeMiddleware>(ResultPattern.Options);
}
