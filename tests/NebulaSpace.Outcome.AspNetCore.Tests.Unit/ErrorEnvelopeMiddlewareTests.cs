using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Tests.Unit.TestHelpers;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

/// <summary>
/// Verifies that <see cref="ErrorEnvelopeResult"/> injects the configured envelope
/// properties (traceId, path, custom) at execution time — the source-injection
/// replacement for the removed buffering middleware.
/// </summary>
public class ErrorEnvelopeResultTests
{
    [Fact]
    public async Task InjectsConfiguredProperties_IntoErrorEnvelope()
    {
        var options = new ResultPatternOptions();
        options.ErrorEnvelope.Add("traceId", _ => "trace-123");

        var result = new ErrorEnvelopeResult(ErrorResponse.NotFound("nope", "not_found"), options);
        var capture = await ResultTestHelper.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        var root = doc.RootElement;
        Assert.Equal(404, root.GetProperty("statusCode").GetInt32());
        Assert.Equal("trace-123", root.GetProperty("traceId").GetString());
        Assert.Equal("nope", root.GetProperty("message").GetString());
        Assert.Equal("not_found", root.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task InjectsMultipleProperties()
    {
        var options = new ResultPatternOptions();
        options.ErrorEnvelope.Add("traceId", _ => "t1");
        options.ErrorEnvelope.Add("path", _ => "/orders");

        var result = new ErrorEnvelopeResult(ErrorResponse.BadRequest("bad"), options);
        var capture = await ResultTestHelper.ExecuteAsync(result);

        using var doc = JsonDocument.Parse(capture.Body);
        Assert.Equal("t1", doc.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("/orders", doc.RootElement.GetProperty("path").GetString());
    }

    [Fact]
    public async Task PropertyValue_CanComeFromHttpContext()
    {
        var options = new ResultPatternOptions();
        options.ErrorEnvelope.Add("path", ctx => ctx.Request.Path.ToString());

        var result = new ErrorEnvelopeResult(ErrorResponse.Conflict("conflict"), options);
        var capture = await ResultTestHelper.ExecuteAsync(
            result,
            ctx => ctx.Request.Path = "/orders/5");

        Assert.Equal(StatusCodes.Status409Conflict, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        Assert.Equal("/orders/5", doc.RootElement.GetProperty("path").GetString());
    }

    [Fact]
    public async Task NullPropertyValue_IsSkipped()
    {
        var options = new ResultPatternOptions();
        options.ErrorEnvelope.Add("traceId", _ => null);

        var result = new ErrorEnvelopeResult(ErrorResponse.NotFound("nope"), options);
        var capture = await ResultTestHelper.ExecuteAsync(result);

        using var doc = JsonDocument.Parse(capture.Body);
        Assert.False(doc.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task UsesEnvelopeStatusCode_AsResponseStatusCode()
    {
        var options = new ResultPatternOptions();
        var result = new ErrorEnvelopeResult(ErrorResponse.UnprocessableEntity("nope"), options);
        var capture = await ResultTestHelper.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, capture.StatusCode);
    }

    [Fact]
    public async Task CustomErrorShape_IsWrittenAsIs_WithInjectedProperties()
    {
        // A shape that is not ErrorResponse (e.g. ProblemDetails) must serialize fully,
        // not just its IErrorResponse interface members.
        var options = new ResultPatternOptions();
        options.ErrorEnvelope.Add("traceId", _ => "trace-1");
        var envelope = new ProblemDetailsLike
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "nope",
        };

        var capture = await ResultTestHelper.ExecuteAsync(
            new ErrorEnvelopeResult(envelope, options));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        var root = doc.RootElement;
        Assert.Equal("nope", root.GetProperty("title").GetString());
        Assert.Equal(422, root.GetProperty("status").GetInt32());
        Assert.Equal("trace-1", root.GetProperty("traceId").GetString());
    }

    /// <summary>Minimal ProblemDetails-like shape implementing <see cref="IErrorResponse"/>.</summary>
    private sealed class ProblemDetailsLike : IErrorResponse
    {
        public int Status { get; set; }
        public string? Title { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extensions { get; set; }

        int IErrorResponse.StatusCode => Status;

        public void AddProperty(string name, object? value)
        {
            Extensions ??= new Dictionary<string, JsonElement>();
            Extensions[name] = JsonSerializer.SerializeToElement(value);
        }
    }
}
