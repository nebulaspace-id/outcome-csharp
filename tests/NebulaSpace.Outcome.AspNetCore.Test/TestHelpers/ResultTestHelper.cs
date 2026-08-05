using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NebulaSpace.Outcome.AspNetCore.Extensions;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Tests.TestHelpers;

/// <summary>
/// Helpers for executing <see cref="IResult"/> objects against an in-memory
/// <see cref="DefaultHttpContext"/> and for (re)configuring the static
/// <see cref="ResultPatternOptions"/> holder used by <see cref="HttpResult"/>.
/// </summary>
internal static class ResultTestHelper
{
    // Framework-built IResult types (Results.Ok/Json/...) resolve JSON options
    // through HttpContext.RequestServices, so provide a provider that has them.
    private static readonly IServiceProvider RequestServices = new ServiceCollection()
        .AddLogging()
        .ConfigureHttpJsonOptions(_ => { })
        .BuildServiceProvider();

    /// <summary>Executes an IResult and captures status code, body, and Location header.</summary>
    public static async Task<ResultCapture> ExecuteAsync(
        IResult result,
        Action<DefaultHttpContext>? configure = null)
    {
        var context = new DefaultHttpContext { RequestServices = RequestServices };
        context.Response.Body = new MemoryStream();
        configure?.Invoke(context);

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        string body;
        using (var reader = new StreamReader(context.Response.Body))
        {
            body = await reader.ReadToEndAsync();
        }

        return new ResultCapture
        {
            StatusCode = context.Response.StatusCode,
            Body = body,
            Location = context.Response.Headers.Location.ToString(),
        };
    }

    /// <summary>Resets the static options to defaults (EnvelopeSuccess = true, traceId/path).</summary>
    public static void ResetOptions() => new ServiceCollection().AddResultPattern();

    /// <summary>Configures the static options used by <see cref="HttpResult"/>.</summary>
    public static void ConfigureOptions(Action<ResultPatternOptions> configure) =>
        new ServiceCollection().AddResultPattern(configure);

    /// <summary>Deserializes a JSON body into a JsonElement (safe even for empty bodies).</summary>
    public static JsonElement ParseJson(string body) =>
        JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body).RootElement.Clone();
}

internal sealed class ResultCapture
{
    public int StatusCode { get; init; }
    public string Body { get; init; } = "";
    public string Location { get; init; } = "";
}
