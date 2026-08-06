using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Extensions;
using NebulaSpace.Outcome.AspNetCore.Failures;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Tests.Unit.TestHelpers;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class HttpResultTests
{
    private static async Task<ResultCapture> Execute(IHttpResultFactory factory, IResult result, string path = "/") =>
        await ResultTestHelper.ExecuteAsync(result, ctx => ctx.Request.Path = path);

    // --- Ok ---

    [Fact]
    public async Task Ok_WithEnvelope_WrapsDataInSuccessResponse()
    {
        var factory = ResultTestHelper.CreateFactory();

        // SuccessEnvelope.Enabled defaults to true.
        var capture = await Execute(factory, factory.Ok(new { greeting = "hi" }));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(200, json.GetProperty("statusCode").GetInt32());
        Assert.Equal("hi", json.GetProperty("data").GetProperty("greeting").GetString());
    }

    [Fact]
    public async Task Ok_WithEnvelopeDisabled_ReturnsRawData()
    {
        var factory = ResultTestHelper.CreateFactory(options => options.SuccessEnvelope.Enabled = false);

        var capture = await Execute(factory, factory.Ok(new { greeting = "hi" }));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("hi", json.GetProperty("greeting").GetString());
        Assert.False(json.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task Ok_WithPagination_IncludesPaginationInEnvelope()
    {
        var factory = ResultTestHelper.CreateFactory();
        var pagination = Pagination.Pagination.Create(1, 10, 37);
        var capture = await Execute(factory, factory.Ok(new[] { 1, 2, 3 }, pagination));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        var page = json.GetProperty("pagination");
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(10, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(4, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(37, page.GetProperty("totalItems").GetInt32());
    }

    // --- Created ---

    [Fact]
    public async Task Created_SetsLocationFromRequestPathAndId()
    {
        var factory = ResultTestHelper.CreateFactory();
        var order = new { Id = Guid.NewGuid() };
        var capture = await Execute(factory, factory.Created(order, o => o.Id), path: "/orders");

        Assert.Equal(StatusCodes.Status201Created, capture.StatusCode);
        Assert.Equal($"/orders/{order.Id}", capture.Location);
    }

    [Fact]
    public async Task Created_WithEnvelope_WrapsDataInSuccessResponse()
    {
        var factory = ResultTestHelper.CreateFactory();
        var order = new { Id = Guid.NewGuid() };
        var capture = await Execute(factory, factory.Created(order, o => o.Id), path: "/orders");

        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(201, json.GetProperty("statusCode").GetInt32());
        Assert.Equal(order.Id.ToString(), json.GetProperty("data").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Created_WithEnvelopeDisabled_ReturnsRawData()
    {
        var factory = ResultTestHelper.CreateFactory(options => options.SuccessEnvelope.Enabled = false);
        var order = new { Id = Guid.NewGuid() };

        var capture = await Execute(factory, factory.Created(order, o => o.Id), path: "/orders");

        Assert.Equal(StatusCodes.Status201Created, capture.StatusCode);
        Assert.Equal($"/orders/{order.Id}", capture.Location);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(order.Id.ToString(), json.GetProperty("id").GetString());
        Assert.False(json.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task Created_TrimsTrailingSlashOnPath()
    {
        var factory = ResultTestHelper.CreateFactory();
        var order = new { Id = 5 };
        var capture = await Execute(factory, factory.Created(order, o => o.Id), path: "/orders/");

        Assert.Equal("/orders/5", capture.Location);
    }

    // --- NoContent ---

    [Fact]
    public async Task NoContent_Returns204WithEmptyBody()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await Execute(factory, factory.NoContent());

        Assert.Equal(StatusCodes.Status204NoContent, capture.StatusCode);
        Assert.Equal("", capture.Body);
    }

    // --- 4xx ---

    [Fact]
    public async Task BadRequest_Sets400AndMessage()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await Execute(factory, factory.BadRequest("oops"));

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("oops", json.GetProperty("detail").GetString());
        Assert.Equal(400, json.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Unauthorized_Sets401()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await Execute(factory, factory.Unauthorized());

        Assert.Equal(StatusCodes.Status401Unauthorized, capture.StatusCode);
    }

    [Fact]
    public async Task Forbidden_Sets403()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await Execute(factory, factory.Forbidden());

        Assert.Equal(StatusCodes.Status403Forbidden, capture.StatusCode);
    }

    [Fact]
    public async Task NotFound_Sets404()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await Execute(factory, factory.NotFound());

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(404, json.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Conflict_Sets409()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await Execute(factory, factory.Conflict());

        Assert.Equal(StatusCodes.Status409Conflict, capture.StatusCode);
    }

    [Fact]
    public async Task ErrorHelpers_WithoutMessage_UseFactoryDefaultReasonPhrase()
    {
        // SOLID: the intent (FailureDetails) may omit the message — the shape factory
        // (DefaultErrorResponseFactory) owns the "default message per status" decision.
        var factory = ResultTestHelper.CreateFactory();

        var capture = await Execute(factory, factory.NotFound());

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Not Found", json.GetProperty("title").GetString());
    }

    [Fact]
    public async Task DefaultErrorBody_OmitsNullDetail_WhenMessageIsNull()
    {
        // factory.NotFound() has no message -> Detail is null and must not appear in the
        // body (OutcomeProblemDetails honors [JsonIgnore(WhenWritingNull)] by default,
        // matching the recommended JSON setup that omits nulls).
        var factory = ResultTestHelper.CreateFactory();

        var capture = await Execute(factory, factory.NotFound());

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.False(json.TryGetProperty("detail", out _));
        Assert.Equal("Not Found", json.GetProperty("title").GetString());
    }

    [Fact]
    public async Task DefaultErrorBody_IsCleanProblemDetails_NoUndeclaredProperties()
    {
        // v0.4.0: the default body is clean RFC 7807 ProblemDetails — no traceId, no path,
        // no statusCode, no errorCode, no custom properties unless configured.
        var factory = ResultTestHelper.CreateFactory();

        var capture = await Execute(factory, factory.NotFound(), path: "/orders/42");

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(404, json.GetProperty("status").GetInt32());
        Assert.Equal("Not Found", json.GetProperty("title").GetString());
        Assert.False(json.TryGetProperty("type", out _));
        Assert.False(json.TryGetProperty("traceId", out _));
        Assert.False(json.TryGetProperty("path", out _));
        Assert.False(json.TryGetProperty("statusCode", out _));
        Assert.False(json.TryGetProperty("errorCode", out _));
    }

    // --- FromFailure ---

    [Fact]
    public async Task FromFailure_NotFound_MapsTo404()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await Execute(factory, factory.FromFailure(new NotFoundFailure("Missing")));

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Missing", json.GetProperty("detail").GetString());
        Assert.Equal("Not Found", json.GetProperty("title").GetString());
    }

    [Fact]
    public async Task FromFailure_Conflict_MapsTo409()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await Execute(factory, factory.FromFailure(new ConflictFailure("Taken")));

        Assert.Equal(StatusCodes.Status409Conflict, capture.StatusCode);
    }

    [Fact]
    public async Task FromFailure_WithConfiguredProperties_InjectsTraceIdAndPath()
    {
        // Simulates the real AddOutcomeHttp flow: default traceId + path envelope properties.
        var factory = ResultTestHelper.CreateFactory(options =>
        {
            options.ErrorEnvelope.Add("traceId", ctx => ctx.TraceIdentifier);
            options.ErrorEnvelope.Add("path", ctx => ctx.Request.Path.ToString());
        });

        var capture = await Execute(factory, factory.FromFailure(new NotFoundFailure("Missing")), path: "/orders/42");

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("/orders/42", json.GetProperty("path").GetString());
        Assert.True(json.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task FromFailure_Validation_MapsTo400()
    {
        var factory = ResultTestHelper.CreateFactory();
        var errors = new Dictionary<string, string> { ["Amount"] = "too small" };
        var capture = await Execute(factory, factory.FromFailure(new ValidationFailure("Invalid", errors)));

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
    }

    [Fact]
    public async Task CustomErrorShapeFactory_CanAddErrorCodeAndErrors()
    {
        // The default body is clean; a custom IErrorResponseFactory decides to add
        // errorCode / errors on top of ProblemDetails — the pluggable path.
        var factory = ResultTestHelper.CreateFactory(new ErrorsProblemDetailsFactory());

        var errors = new Dictionary<string, string> { ["Amount"] = "too small" };
        var capture = await Execute(factory, factory.FromFailure(new ValidationFailure("Invalid", errors)));

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("validation", json.GetProperty("errorCode").GetString());
        Assert.Equal("too small", json.GetProperty("errors").GetProperty("Amount").GetString());
    }

    [Fact]
    public async Task FromFailure_BadRequest_MapsTo400()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await Execute(factory, factory.FromFailure(new BadRequestFailure("Bad")));

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
    }

    [Fact]
    public async Task FromFailure_Unauthorized_MapsTo401()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await Execute(factory, factory.FromFailure(new UnauthorizedFailure("Auth")));

        Assert.Equal(StatusCodes.Status401Unauthorized, capture.StatusCode);
    }

    [Fact]
    public async Task FromFailure_Forbidden_MapsTo403()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await Execute(factory, factory.FromFailure(new ForbiddenFailure("Denied")));

        Assert.Equal(StatusCodes.Status403Forbidden, capture.StatusCode);
    }

    [Fact]
    public async Task FromFailure_UnknownFailure_Returns500()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await Execute(factory, factory.FromFailure(new UnknownFailureStub()));

        Assert.Equal(StatusCodes.Status500InternalServerError, capture.StatusCode);
    }

    // --- Custom Failures.Map ---

    [Fact]
    public async Task FromFailure_CustomMappedFailure_UsesCustomResult()
    {
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Failures.Map<QuotaStub>(f =>
                ErrorResponse.UnprocessableEntity(f.Message, f.Code)));

        var capture = await Execute(factory, factory.FromFailure(new QuotaStub("over limit")));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("over limit", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task FromFailure_UsingFailuresSectionMap_Works()
    {
        // Section-based API: options.Failures.Map<T>(...).
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Failures.Map<QuotaStub>(f =>
                ErrorResponse.UnprocessableEntity(f.Message, f.Code)));

        var capture = await Execute(factory, factory.FromFailure(new QuotaStub("over limit")));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("over limit", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task FromFailure_CustomMapper_OverridesBuiltInIHttpFailure()
    {
        // NotFoundFailure implements IHttpFailure; an explicit Failures.Map must win.
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Failures.Map<NotFoundFailure>(f =>
                ErrorResponse.UnprocessableEntity(f.Message, f.Code)));

        var capture = await Execute(factory, factory.FromFailure(new NotFoundFailure("Missing")));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, capture.StatusCode);
    }

    [Fact]
    public async Task FromFailure_DerivedType_UsesNearestBaseTypeMapper()
    {
        // DerivedQuota inherits QuotaStub; the mapper registered for QuotaStub must apply.
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Failures.Map<QuotaStub>(f =>
                ErrorResponse.UnprocessableEntity(f.Message, f.Code)));

        var capture = await Execute(factory, factory.FromFailure(new DerivedQuota("over limit")));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("over limit", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task FromFailure_WithCustomErrorShapeFactory_ProducesProblemDetails()
    {
        // Replacing IErrorResponseFactory changes the shape of every error response —
        // here to RFC 7807 ProblemDetails — while per-request properties still inject.
        var options = new ResultPatternOptions();
        options.ErrorEnvelope.Add("traceId", ctx => ctx.TraceIdentifier);
        var factory = new HttpResultFactory(options, new ProblemDetailsErrorResponseFactory());

        var capture = await Execute(factory, factory.FromFailure(new NotFoundFailure("Missing")));

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Not Found", json.GetProperty("title").GetString());
        Assert.Equal("Missing", json.GetProperty("detail").GetString());
        Assert.Equal(404, json.GetProperty("status").GetInt32());
        Assert.True(json.TryGetProperty("traceId", out _)); // still injected
    }

    [Fact]
    public async Task AddOutcomeHttp_ProducesCleanProblemDetails_ByDefault()
    {
        // v0.4.0: AddOutcomeHttp alone (no Replace) now yields RFC 7807 ProblemDetails
        // with a clean body — no traceId/path unless configured.
        var services = new ServiceCollection();
        services.AddOutcomeHttp();

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpResultFactory>();

        var capture = await Execute(factory, factory.NotFound());

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Not Found", json.GetProperty("title").GetString());
        Assert.Equal(404, json.GetProperty("status").GetInt32());
        Assert.False(json.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task AddOutcomeHttp_ReplaceFactory_ChangesEveryErrorShape()
    {
        // Replace() swaps the shape for ALL error helpers: swap to a legacy
        // ErrorResponse-style body and verify FromFailure follows.
        var services = new ServiceCollection();
        services.AddOutcomeHttp();
        services.Replace(ServiceDescriptor.Singleton<IErrorResponseFactory, LegacyErrorResponseFactory>());

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpResultFactory>();

        var capture = await Execute(factory, factory.FromFailure(new NotFoundFailure("Missing")));

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Missing", json.GetProperty("message").GetString());
        Assert.Equal("not_found", json.GetProperty("errorCode").GetString());
    }

    /// <summary>RFC 7807 ProblemDetails-style error shape via the pluggable factory.</summary>
    private sealed class ProblemDetailsErrorResponseFactory : IErrorResponseFactory
    {
        public IErrorResponse Create(FailureDetails details) => new ProblemDetailsLike
        {
            Status = details.StatusCode,
            Title = ReasonPhrases.GetReasonPhrase(details.StatusCode),
            Detail = details.Message,
            ErrorCode = details.Code,
        };
    }

    private sealed class ProblemDetailsLike : IErrorResponse
    {
        public int Status { get; set; }
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public string? ErrorCode { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extensions { get; set; }

        int IErrorResponse.StatusCode => Status;

        public void AddProperty(string name, object? value)
        {
            Extensions ??= new Dictionary<string, JsonElement>();
            Extensions[name] = JsonSerializer.SerializeToElement(value);
        }
    }

    /// <summary>Default shape + adds errorCode / field errors on top of ProblemDetails.</summary>
    private sealed class ErrorsProblemDetailsFactory : IErrorResponseFactory
    {
        public IErrorResponse Create(FailureDetails details)
        {
            var problemDetails = new OutcomeProblemDetails
            {
                Title = ReasonPhrases.GetReasonPhrase(details.StatusCode),
                Status = details.StatusCode,
                Detail = details.Message,
            };
            if (details.Code is not null)
            {
                problemDetails.Extensions["errorCode"] = details.Code;
            }
            if (details.Errors is { } errors)
            {
                problemDetails.Extensions["errors"] = errors;
            }
            return problemDetails;
        }
    }

    /// <summary>Legacy ErrorResponse-style body, used to prove Replace() changes the shape.</summary>
    private sealed class LegacyErrorResponseFactory : IErrorResponseFactory
    {
        public IErrorResponse Create(FailureDetails details) => new ErrorResponse
        {
            StatusCode = details.StatusCode,
            Message = details.Message ?? ReasonPhrases.GetReasonPhrase(details.StatusCode),
            ErrorCode = details.Code,
        };
    }

    private sealed class UnknownFailureStub : Failure
    {
        public UnknownFailureStub() : base("boom", "unknown") { }
    }

    private class QuotaStub : Failure
    {
        public QuotaStub(string message) : base(message, "quota") { }
    }

    private sealed class DerivedQuota : QuotaStub
    {
        public DerivedQuota(string message) : base(message) { }
    }
}
