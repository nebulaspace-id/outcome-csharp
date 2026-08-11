using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Tests.Unit.TestHelpers;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

/// <summary>
/// Verifies the typed error envelope: <see cref="ErrorEnvelopeResult"/> renders the
/// user-defined envelope type via <c>options.Error.SetResponse&lt;AppError&gt;()</c> (built from
/// the failure's HTTP intent and the request), writes the status from
/// <see cref="FailureDetails"/>, and falls back to the default RFC 7807
/// <see cref="DefaultErrorResponse"/> when no envelope is configured.
/// </summary>
public class ErrorEnvelopeResultTests
{
    [Fact]
    public async Task DefaultEnvelope_IsCleanProblemDetails_WhenNotConfigured()
    {
        var options = new OutcomeOptions();
        var result = new ErrorEnvelopeResult(
            new FailureDetails(StatusCodes.Status404NotFound, "nope", "not_found"), options);

        var capture = await ResultTestHelper.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        var root = doc.RootElement;
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("Not Found", root.GetProperty("title").GetString());
        Assert.Equal("nope", root.GetProperty("detail").GetString());
        Assert.False(root.TryGetProperty("traceId", out _));
        Assert.False(root.TryGetProperty("path", out _));
    }

    [Fact]
    public async Task TypedEnvelope_IsSerializedAsUserType_WithPerRequestFields()
    {
        var options = new OutcomeOptions();
        options.Error.SetResponse<AppError>();

        var result = new ErrorEnvelopeResult(
            new FailureDetails(StatusCodes.Status404NotFound, "nope", "not_found"), options);
        var capture = await ResultTestHelper.ExecuteAsync(result, ctx => ctx.Request.Path = "/orders/42");

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        var root = doc.RootElement;
        Assert.Equal("not_found", root.GetProperty("code").GetString());
        Assert.Equal("nope", root.GetProperty("message").GetString());
        Assert.Equal("/orders/42", root.GetProperty("path").GetString());
        Assert.True(root.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task TypedEnvelope_WritesStatusFromFailureDetails_NotFromEnvelope()
    {
        // The envelope is pure body — the HTTP status comes from FailureDetails.
        var options = new OutcomeOptions();
        options.Error.SetResponse<AppError>();

        var result = new ErrorEnvelopeResult(
            new FailureDetails(StatusCodes.Status409Conflict, "conflict"), options);
        var capture = await ResultTestHelper.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status409Conflict, capture.StatusCode);
    }

    [Fact]
    public async Task TypedEnvelope_PropertyCanComeFromHttpContext()
    {
        var options = new OutcomeOptions();
        options.Error.SetResponse<AppError>();

        var result = new ErrorEnvelopeResult(
            new FailureDetails(StatusCodes.Status404NotFound, "nope"), options);
        var capture = await ResultTestHelper.ExecuteAsync(result, ctx => ctx.Request.Path = "/orders/5");

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        Assert.Equal("/orders/5", doc.RootElement.GetProperty("path").GetString());
    }

    [Fact]
    public async Task TypedEnvelope_ViaInterface_SetType_SerializesUserType()
    {
        // The symmetric API: pass the envelope TYPE, which implements IErrorEnvelope.
        // The library invokes its static From(FailureDetails, HttpContext).
        var options = new OutcomeOptions();
        options.Error.SetResponse<AppError>();

        var result = new ErrorEnvelopeResult(
            new FailureDetails(StatusCodes.Status404NotFound, "nope", "not_found"), options);
        var capture = await ResultTestHelper.ExecuteAsync(result, ctx => ctx.Request.Path = "/orders/42");

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        var root = doc.RootElement;
        Assert.Equal("not_found", root.GetProperty("code").GetString());
        Assert.Equal("nope", root.GetProperty("message").GetString());
        Assert.Equal("/orders/42", root.GetProperty("path").GetString());
    }

    [Fact]
    public void Set_EnvelopeNotImplementingInterface_ThrowsInvalidOperationException()
    {
        var options = new OutcomeOptions();

        var ex = Assert.Throws<InvalidOperationException>(() => options.Error.SetResponse(typeof(object)));
        Assert.Contains("IErrorEnvelope", ex.Message);
    }

    [Fact]
    public async Task TypedEnvelope_WithValidationErrors_SerializesFieldErrors()
    {
        var options = new OutcomeOptions();
        options.Error.SetResponse<AppError>();

        var errors = new Dictionary<string, string[]> { ["Amount"] = ["too small"] };
        var result = new ErrorEnvelopeResult(
            new FailureDetails(StatusCodes.Status400BadRequest, "Validation Error", errors: errors), options);
        var capture = await ResultTestHelper.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        var amount = doc.RootElement.GetProperty("fieldErrors").GetProperty("Amount");
        Assert.Equal("too small", amount[0].GetString());
        Assert.Single(amount.EnumerateArray());
    }

    /// <summary>A user-defined typed envelope — implements IErrorEnvelope, serializes fully.</summary>
    private sealed class AppError : IErrorEnvelope
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
        public string? TraceId { get; set; }
        public string? Path { get; set; }
        public IReadOnlyDictionary<string, string[]>? FieldErrors { get; set; }

        /// <summary>Builds the per-request error body from the failure intent and the request.</summary>
        public static IErrorEnvelope From(FailureDetails details, HttpContext ctx) => new AppError
        {
            Code = details.Code,
            Message = details.Message,
            TraceId = ctx.TraceIdentifier,
            Path = ctx.Request.Path.ToString(),
            FieldErrors = details.Errors,
        };
    }
}
