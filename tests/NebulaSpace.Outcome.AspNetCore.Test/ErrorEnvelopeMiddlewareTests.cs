using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Middleware;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Tests.TestHelpers;

namespace NebulaSpace.Outcome.AspNetCore.Tests;

public class ErrorEnvelopeMiddlewareTests
{
    private static ErrorEnvelopeMiddleware Build(Action<ResultPatternOptions>? configure = null)
    {
        var options = new ResultPatternOptions();
        configure?.Invoke(options);
        return new ErrorEnvelopeMiddleware(_ => Task.CompletedTask, options);
    }

    private static ErrorEnvelopeMiddleware BuildWithBody(
        int statusCode,
        string body,
        string contentType = "application/json",
        Action<ResultPatternOptions>? configure = null)
    {
        var options = new ResultPatternOptions();
        configure?.Invoke(options);

        return new ErrorEnvelopeMiddleware(async context =>
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = contentType;
            await context.Response.WriteAsync(body);
        }, options);
    }

    private static async Task<(int Status, string Body)> RunAsync(ErrorEnvelopeMiddleware middleware)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task InjectsConfiguredProperties_IntoErrorEnvelope()
    {
        var middleware = BuildWithBody(
            StatusCodes.Status404NotFound,
            """{"statusCode":404,"message":"nope"}""",
            configure: options => options.AddErrorEnvelopeProperty("traceId", _ => "trace-123"));

        var (status, body) = await RunAsync(middleware);

        Assert.Equal(StatusCodes.Status404NotFound, status);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal(404, root.GetProperty("statusCode").GetInt32());
        Assert.Equal("trace-123", root.GetProperty("traceId").GetString());
        Assert.Equal("nope", root.GetProperty("message").GetString());
    }

    [Fact]
    public async Task InjectsMultipleProperties()
    {
        var middleware = BuildWithBody(
            StatusCodes.Status400BadRequest,
            """{"statusCode":400,"message":"bad"}""",
            configure: options =>
            {
                options.AddErrorEnvelopeProperty("traceId", _ => "t1");
                options.AddErrorEnvelopeProperty("path", _ => "/orders");
            });

        var (_, body) = await RunAsync(middleware);

        using var doc = JsonDocument.Parse(body);
        Assert.Equal("t1", doc.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("/orders", doc.RootElement.GetProperty("path").GetString());
    }

    [Fact]
    public async Task PassesThrough_WhenStatusCodeBelow400()
    {
        var middleware = BuildWithBody(
            StatusCodes.Status200OK,
            """{"statusCode":200,"data":42}""",
            configure: options => options.AddErrorEnvelopeProperty("traceId", _ => "t1"));

        var (status, body) = await RunAsync(middleware);

        Assert.Equal(StatusCodes.Status200OK, status);
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.TryGetProperty("traceId", out _));
        Assert.Equal(42, doc.RootElement.GetProperty("data").GetInt32());
    }

    [Fact]
    public async Task PassesThrough_WhenNoPropertiesConfigured()
    {
        var middleware = BuildWithBody(
            StatusCodes.Status404NotFound,
            """{"statusCode":404,"message":"x"}""");

        var (status, body) = await RunAsync(middleware);

        Assert.Equal(StatusCodes.Status404NotFound, status);
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task PassesThrough_WhenBodyIsNotJsonEnvelope()
    {
        var middleware = BuildWithBody(
            StatusCodes.Status400BadRequest,
            "plain text",
            contentType: "text/plain",
            configure: options => options.AddErrorEnvelopeProperty("traceId", _ => "t1"));

        var (status, body) = await RunAsync(middleware);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("plain text", body);
    }

    [Fact]
    public async Task PassesThrough_WhenJsonBodyHasNoStatusCodeAbove400()
    {
        var middleware = BuildWithBody(
            StatusCodes.Status401Unauthorized,
            """{"message":"unauthorized"}""",
            configure: options => options.AddErrorEnvelopeProperty("traceId", _ => "t1"));

        var (_, body) = await RunAsync(middleware);

        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.TryGetProperty("traceId", out _));
        Assert.Equal("unauthorized", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task PropertyValue_CanComeFromHttpContext()
    {
        var middleware = BuildWithBody(
            StatusCodes.Status409Conflict,
            """{"statusCode":409,"message":"conflict"}""",
            configure: options => options.AddErrorEnvelopeProperty("path", ctx => ctx.Request.Path.ToString()));

        var context = new DefaultHttpContext { Request = { Path = "/orders/5" } };
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("/orders/5", doc.RootElement.GetProperty("path").GetString());
    }
}
