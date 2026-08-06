using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NebulaSpace.Outcome.AspNetCore.Extensions;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Middleware;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Integration;

/// <summary>
/// Verifies that <see cref="OutcomeExceptionMiddleware"/> intercepts unhandled exceptions
/// and produces the SAME error envelope as expected failures (via IHttpResultFactory), so
/// every error shares one shape.
/// </summary>
public class OutcomeExceptionMiddlewareTests
{
    private static async Task<TestHostScope> CreateHost(
        Action<IServiceCollection>? configureServices = null,
        RequestDelegate? endpoint = null,
        bool useMiddleware = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddOutcomeHttp(options =>
        {
            options.ErrorEnvelope.Add("traceId", ctx => ctx.TraceIdentifier);
        });
        configureServices?.Invoke(builder.Services);
        if (useMiddleware)
        {
            // Idempotent: no-op if configureServices already registered it.
            builder.Services.AddOutcomeExceptionHandler();
        }

        var app = builder.Build();

        if (useMiddleware)
        {
            app.UseOutcomeExceptionHandler();
        }
        app.Run(endpoint ?? (_ => throw new InvalidOperationException("boom")));

        await app.StartAsync();

        return new TestHostScope(app, app.GetTestClient());
    }

    private static async Task<HttpResponseMessage> SendAsync(
        Action<IServiceCollection>? configureServices = null,
        RequestDelegate? endpoint = null,
        bool useMiddleware = true)
    {
        await using var scope = await CreateHost(configureServices, endpoint, useMiddleware);
        return await scope.Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/"));
    }

    [Fact]
    public async Task UnhandledException_ReturnsSameEnvelope_Status500()
    {
        await using var scope = await CreateHost();
        using var response = await scope.Client.GetAsync("/");

        Assert.Equal(StatusCodes.Status500InternalServerError, (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal("Internal Server Error", root.GetProperty("title").GetString());
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("An error occurred while processing your request.", root.GetProperty("detail").GetString());
        Assert.True(root.TryGetProperty("traceId", out _)); // envelope property injected
    }

    [Fact]
    public async Task UnhandledException_DoesNotLeakExceptionMessage_ByDefault()
    {
        await using var scope = await CreateHost(endpoint: _ => throw new InvalidOperationException("SECRET-internal-detail"));
        using var response = await scope.Client.GetAsync("/");

        Assert.Equal(StatusCodes.Status500InternalServerError, (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        // The full exception is logged server-side; the body stays generic.
        Assert.Equal("An error occurred while processing your request.", doc.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("SECRET-internal-detail", body);
    }

    [Fact]
    public async Task IncludeExceptionDetails_RevealsMessage()
    {
        await using var scope = await CreateHost(
            configureServices: services => services.AddOutcomeExceptionHandler(o => o.IncludeExceptionDetails = true),
            endpoint: _ => throw new InvalidOperationException("boom detail"));
        using var response = await scope.Client.GetAsync("/");

        Assert.Equal(StatusCodes.Status500InternalServerError, (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("boom detail", doc.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task SuccessfulRequest_IsNotAffected()
    {
        await using var scope = await CreateHost(endpoint: async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            await ctx.Response.WriteAsync("ok");
        });
        using var response = await scope.Client.GetAsync("/");

        Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithoutMiddleware_Exception_PropagatesOut()
    {
        // Without the middleware, an unhandled exception is NOT converted into our envelope —
        // it bubbles up (TestHost surfaces it to the caller). The middleware is what converts
        // it into the same ProblemDetails envelope as expected failures.
        await using var scope = await CreateHost(useMiddleware: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scope.Client.GetAsync("/"));
    }

    // --- Mapped exceptions (options.Exceptions.Map<TException>) ---

    [Fact]
    public async Task MappedException_ReturnsCustomEnvelope()
    {
        await using var scope = await CreateHost(
            configureServices: services => services.AddOutcomeExceptionHandler(o =>
                o.Exceptions.Map<DomainExceptionStub>(ex =>
                    ErrorResponse.UnprocessableEntity(ex.Message, ex.Code))),
            endpoint: _ => throw new DomainExceptionStub("order blocked", "domain"));

        using var response = await scope.Client.GetAsync("/");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, (int)response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("order blocked", root.GetProperty("message").GetString());
        Assert.Equal("domain", root.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task MappedException_BypassesIncludeExceptionDetailsGating()
    {
        // IncludeExceptionDetails=false is ignored for mapped exceptions: the mapper's
        // envelope is authoritative, even though it leaks ex.Message (here, intentionally).
        await using var scope = await CreateHost(
            configureServices: services => services.AddOutcomeExceptionHandler(o =>
                o.Exceptions.Map<DomainExceptionStub>(ex =>
                    ErrorResponse.UnprocessableEntity(ex.Message))),
            endpoint: _ => throw new DomainExceptionStub("SECRET mapped detail", "domain"));

        using var response = await scope.Client.GetAsync("/");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, (int)response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        // The mapper chose to include the message — IncludeExceptionDetails is irrelevant.
        Assert.Equal("SECRET mapped detail", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task MappedException_StillGetsEnvelopeProperties()
    {
        await using var scope = await CreateHost(
            configureServices: services => services.AddOutcomeExceptionHandler(o =>
                o.Exceptions.Map<DomainExceptionStub>(ex => ErrorResponse.NotFound(ex.Message))),
            endpoint: _ => throw new DomainExceptionStub("nope", "domain"));

        using var response = await scope.Client.GetAsync("/");

        Assert.Equal(StatusCodes.Status404NotFound, (int)response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        // traceId envelope property registered in CreateHost still injects.
        Assert.True(doc.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task DerivedExceptionType_UsesNearestBaseMapper()
    {
        // DerivedDomainException inherits DomainExceptionStub; the mapper registered for
        // DomainExceptionStub must apply.
        await using var scope = await CreateHost(
            configureServices: services => services.AddOutcomeExceptionHandler(o =>
                o.Exceptions.Map<DomainExceptionStub>(ex => ErrorResponse.Conflict(ex.Message))),
            endpoint: _ => throw new DerivedDomainExceptionStub("conflict", "conflict_code"));

        using var response = await scope.Client.GetAsync("/");

        Assert.Equal(StatusCodes.Status409Conflict, (int)response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("conflict", doc.RootElement.GetProperty("message").GetString());
    }

    /// <summary>Keeps the TestServer + client alive together for the duration of a test.</summary>
    private sealed class TestHostScope : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public TestHostScope(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
        }
    }

    /// <summary>Stands in for an application's domain exception.</summary>
    private class DomainExceptionStub : Exception
    {
        /// <summary>Machine-readable error code, supplied at throw time.</summary>
        public string Code { get; }

        public DomainExceptionStub(string message, string code) : base(message)
        {
            Code = code;
        }
    }

    /// <summary>A subclass of <see cref="DomainExceptionStub"/> for inheritance mapping tests.</summary>
    private sealed class DerivedDomainExceptionStub : DomainExceptionStub
    {
        public DerivedDomainExceptionStub(string message, string code) : base(message, code) { }
    }
}
