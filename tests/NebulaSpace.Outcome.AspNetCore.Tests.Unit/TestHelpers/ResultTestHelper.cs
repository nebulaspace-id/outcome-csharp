using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit.TestHelpers;

/// <summary>
/// Helpers for executing <see cref="IResult"/> objects against an in-memory
/// <see cref="DefaultHttpContext"/>. No static state is shared between tests,
/// so test collections may run in parallel.
/// </summary>
internal static class ResultTestHelper
{
    // Framework-built IResult types (Results.Ok/Json/...) resolve JSON options
    // through HttpContext.RequestServices, so provide a provider that has them.
    private static readonly IServiceProvider RequestServices = new ServiceCollection()
        .AddLogging()
        .ConfigureHttpJsonOptions(_ => { })
        .BuildServiceProvider();

    /// <summary>Creates a fresh factory with default options (SuccessEnvelope.Enabled = true).</summary>
    public static HttpResultFactory CreateFactory() =>
        new(new ResultPatternOptions());

    /// <summary>Creates a fresh factory with custom options.</summary>
    public static HttpResultFactory CreateFactory(Action<ResultPatternOptions> configure)
    {
        var options = new ResultPatternOptions();
        configure(options);
        return new HttpResultFactory(options);
    }

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

    /// <summary>Deserializes a JSON body into a JsonElement (safe even for empty bodies).</summary>
    public static JsonElement ParseJson(string body) =>
        JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body).RootElement.Clone();
}