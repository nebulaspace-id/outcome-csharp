using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Extensions;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Tests.Unit.TestHelpers;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

/// <summary>
/// Covers responses the framework produces without running application code: model-state
/// 400s, routing 404s, method 405s, auth 401/403s and rate-limiter 429s. Every one of them
/// must render the configured error envelope, so clients never need a second error parser.
/// </summary>
public class FrameworkErrorConsistencyTests
{
    private sealed class TeapotFailure : Failure
    {
        public TeapotFailure() : base("short and stout", "teapot") { }
    }

    private sealed class TestErrorEnvelope : IErrorEnvelope
    {
        public string Code { get; init; } = "";
        public string Message { get; init; } = "";
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

        public static IErrorEnvelope From(FailureDetails details, HttpContext httpContext) => new TestErrorEnvelope
        {
            Code = details.Code ?? "",
            Message = details.Message ?? "",
            Errors = details.Errors,
        };
    }

    private static ServiceProvider CreateProvider(Action<OutcomeOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });
        services.AddOutcome(configure);
        services.AddControllers();
        services.AddOutcomeMvcFilter();
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(ServiceProvider provider, string path = "/")
    {
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpResponse response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        return await reader.ReadToEndAsync();
    }

    // ── Base-type mapper lookup ──────────────────────────────────────────

    [Fact]
    public async Task MapFailure_OnBaseFailureType_MatchesDerivedFailure()
    {
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Error.MapFailure<Failure>(failure =>
                new FailureDetails(418, failure.Message, failure.Code)));

        var capture = await ResultTestHelper.ExecuteAsync(factory.FromFailure(new TeapotFailure()));

        Assert.Equal(418, capture.StatusCode);

        using var json = JsonDocument.Parse(capture.Body);
        Assert.Equal("short and stout", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task MapFailure_OnDerivedType_StillWinsOverBaseType()
    {
        var factory = ResultTestHelper.CreateFactory(options =>
        {
            options.Error.MapFailure<Failure>(_ => new FailureDetails(418, "base", "base"));
            options.Error.MapFailure<TeapotFailure>(_ => new FailureDetails(409, "derived", "derived"));
        });

        var capture = await ResultTestHelper.ExecuteAsync(factory.FromFailure(new TeapotFailure()));

        Assert.Equal(409, capture.StatusCode);

        using var json = JsonDocument.Parse(capture.Body);
        Assert.Equal("derived", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public void MapException_OnBaseExceptionType_MatchesDerivedException()
    {
        var options = new OutcomeOptions();
        options.Error.MapException<Exception>(exception =>
            new FailureDetails(409, $"mapped: {exception.Message}", "mapped"));

        Assert.True(options.TryGetExceptionMapper(typeof(InvalidOperationException), out var mapper));

        var details = mapper(new InvalidOperationException("boom"));

        Assert.Equal(409, details.StatusCode);
        Assert.Equal("mapped: boom", details.Message);
    }

    // ── Model-state 400 ──────────────────────────────────────────────────

    [Fact]
    public async Task InvalidModelState_UsesConfiguredErrorEnvelope()
    {
        using var provider = CreateProvider();
        var behavior = provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;

        var http = CreateContext(provider);
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        actionContext.ModelState.AddModelError("name", "The Name field is required.");
        actionContext.ModelState.AddModelError("price", "The Price field must be greater than 0.");

        var result = behavior.InvalidModelStateResponseFactory(actionContext);
        await result.ExecuteResultAsync(actionContext);

        Assert.Equal(StatusCodes.Status400BadRequest, http.Response.StatusCode);

        using var json = JsonDocument.Parse(await ReadBodyAsync(http.Response));
        var root = json.RootElement;

        // RFC 7807 default envelope — not ASP.NET Core's ValidationProblemDetails.
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("One or more validation errors occurred.", root.GetProperty("detail").GetString());
        Assert.Equal("The Name field is required.", root.GetProperty("errors").GetProperty("name")[0].GetString());
        Assert.False(root.TryGetProperty("title", out var title) && title.GetString() == "One or more validation errors occurred.");
    }

    [Fact]
    public async Task InvalidModelState_WithTypedEnvelope_UsesTypedEnvelope()
    {
        using var provider = CreateProvider(options => options.Error.SetResponse<TestErrorEnvelope>());
        var behavior = provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;

        var http = CreateContext(provider);
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        actionContext.ModelState.AddModelError("name", "The Name field is required.");

        var result = behavior.InvalidModelStateResponseFactory(actionContext);
        await result.ExecuteResultAsync(actionContext);

        Assert.Equal(StatusCodes.Status400BadRequest, http.Response.StatusCode);

        using var json = JsonDocument.Parse(await ReadBodyAsync(http.Response));
        var root = json.RootElement;

        Assert.Equal("validation_error", root.GetProperty("code").GetString());
        Assert.Equal("One or more validation errors occurred.", root.GetProperty("message").GetString());
        Assert.Equal("The Name field is required.", root.GetProperty("errors").GetProperty("name")[0].GetString());
    }

    // ── Framework-generated status codes ─────────────────────────────────

    [Fact]
    public async Task StatusCodePages_404_WritesErrorEnvelope()
    {
        using var provider = CreateProvider();
        var app = new ApplicationBuilder(provider);
        app.UseOutcomeStatusCodePages();
        var pipeline = app.Build();

        var context = CreateContext(provider, "/missing");
        await pipeline(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        using var json = JsonDocument.Parse(await ReadBodyAsync(context.Response));
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("The requested resource was not found.", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task StatusCodePages_UsesMappedStatusCodeDefaults()
    {
        using var provider = CreateProvider(options =>
            options.Error.MapStatusCode(StatusCodes.Status405MethodNotAllowed, "no_such_method", "Nope."));

        var app = new ApplicationBuilder(provider);
        app.UseOutcomeStatusCodePages();
        app.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();

        var context = CreateContext(provider, "/products");
        await pipeline(context);

        Assert.Equal(StatusCodes.Status405MethodNotAllowed, context.Response.StatusCode);

        using var json = JsonDocument.Parse(await ReadBodyAsync(context.Response));
        Assert.Equal("Nope.", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task StatusCodePages_SkipsExcludedPaths()
    {
        using var provider = CreateProvider();
        var app = new ApplicationBuilder(provider);
        app.UseOutcomeStatusCodePages();
        var pipeline = app.Build();

        var context = CreateContext(provider, "/swagger/index.html");
        await pipeline(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal(string.Empty, await ReadBodyAsync(context.Response));
    }

    [Fact]
    public async Task StatusCodePages_SkipsHeadRequests()
    {
        using var provider = CreateProvider();
        var app = new ApplicationBuilder(provider);
        app.UseOutcomeStatusCodePages();
        var pipeline = app.Build();

        var context = CreateContext(provider, "/missing");
        context.Request.Method = HttpMethods.Head;
        await pipeline(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal(string.Empty, await ReadBodyAsync(context.Response));
    }

    [Fact]
    public void StatusCodePages_WithoutAddOutcome_ThrowsClearError()
    {
        using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var app = new ApplicationBuilder(provider);

        var exception = Assert.Throws<InvalidOperationException>(() => app.UseOutcomeStatusCodePages());
        Assert.Contains("AddOutcome", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorOptions_StatusCodes_RejectOutOfRangeValues()
    {
        var options = new OutcomeOptions();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => options.Error.MapStatusCode(200, "ok", "Not an error."));
        Assert.Throws<ArgumentException>(
            () => options.Error.MapStatusCode(404, " ", "Missing code."));
    }

    // ── Unit outcomes ────────────────────────────────────────────────────

    [Fact]
    public async Task UnitOutcome_Success_ToResult_Is204()
    {
        var factory = ResultTestHelper.CreateFactory();

        var capture = await ResultTestHelper.ExecuteAsync(Outcome.Success().ToResult(factory));

        Assert.Equal(StatusCodes.Status204NoContent, capture.StatusCode);
        Assert.Equal(string.Empty, capture.Body);
    }

    [Fact]
    public async Task UnitOutcome_Success_ToActionResult_Is204()
    {
        var factory = ResultTestHelper.CreateFactory();

        var capture = await ResultTestHelper.ExecuteActionResultAsync(
            Outcome.Success().ToActionResult(factory));

        Assert.Equal(StatusCodes.Status204NoContent, capture.StatusCode);
        Assert.Equal(string.Empty, capture.Body);
    }

    [Fact]
    public async Task UnitOutcome_Failure_StillBecomesErrorEnvelope()
    {
        var factory = ResultTestHelper.CreateFactory();
        var outcome = Outcome.Fail(new TeapotFailure());

        var capture = await ResultTestHelper.ExecuteAsync(outcome.ToResult(factory));

        Assert.Equal(StatusCodes.Status500InternalServerError, capture.StatusCode);

        using var json = JsonDocument.Parse(capture.Body);
        Assert.Equal("An error occurred while processing your request.", json.RootElement.GetProperty("detail").GetString());
    }
}
