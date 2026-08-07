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
        var pagination = Pagination.PageInfo.Create(1, 10, 37);
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
        // The default error envelope owns the "default message per status" decision.
        var factory = ResultTestHelper.CreateFactory();

        var capture = await Execute(factory, factory.NotFound());

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Not Found", json.GetProperty("title").GetString());
    }

    [Fact]
    public async Task DefaultErrorBody_WritesEmptyDetail_WhenMessageIsNull()
    {
        // factory.NotFound() has no message -> Detail is "" (DefaultProblemDetails is
        // non-nullable, so detail always appears in the body).
        var factory = ResultTestHelper.CreateFactory();

        var capture = await Execute(factory, factory.NotFound());

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(string.Empty, json.GetProperty("detail").GetString());
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
    public async Task FromFailure_WithConfiguredTypedEnvelope_RendersIt()
    {
        // The real AddOutcomeHttp flow: a typed envelope configured via ErrorEnvelope.Set<T>.
        var factory = ResultTestHelper.CreateFactory(options =>
            options.ErrorEnvelope.Set<AppError>((details, ctx) => new AppError
            {
                Code = details.Code,
                Message = details.Message,
                TraceId = ctx.TraceIdentifier,
                Path = ctx.Request.Path.ToString(),
            }));

        var capture = await Execute(factory, factory.FromFailure(new NotFoundFailure("Missing")), path: "/orders/42");

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("/orders/42", json.GetProperty("path").GetString());
        Assert.Equal("not_found", json.GetProperty("code").GetString());
        Assert.True(json.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task FromFailure_Validation_MapsTo400()
    {
        var factory = ResultTestHelper.CreateFactory();
        var errors = new Dictionary<string, string[]> { ["Amount"] = ["too small"] };
        var capture = await Execute(factory, factory.FromFailure(new ValidationFailure("Invalid", errors)));

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
    }

    [Fact]
    public async Task CustomTypedEnvelope_CanAddErrorCodeAndErrors()
    {
        // The default body is clean; a typed envelope decides to add code / field errors.
        var factory = ResultTestHelper.CreateFactory(options =>
            options.ErrorEnvelope.Set<AppError>((details, ctx) => new AppError
            {
                Code = details.Code,
                Message = details.Message,
                FieldErrors = details.Errors,
            }));

        var errors = new Dictionary<string, string[]> { ["Amount"] = ["too small"] };
        var capture = await Execute(factory, factory.FromFailure(new ValidationFailure("Invalid", errors)));

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("validation", json.GetProperty("code").GetString());
        var amount = json.GetProperty("fieldErrors").GetProperty("Amount");
        Assert.Equal("too small", amount[0].GetString());
        Assert.Single(amount.EnumerateArray());
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
                new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code)));

        var capture = await Execute(factory, factory.FromFailure(new QuotaStub("over limit")));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("over limit", json.GetProperty("detail").GetString());
        Assert.Equal(422, json.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task FromFailure_UsingFailuresSectionMap_Works()
    {
        // Section-based API: options.Failures.Map<T>(...).
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Failures.Map<QuotaStub>(f =>
                new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code)));

        var capture = await Execute(factory, factory.FromFailure(new QuotaStub("over limit")));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("over limit", json.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task FromFailure_CustomMapper_OverridesBuiltInIHttpFailure()
    {
        // NotFoundFailure implements IHttpFailure; an explicit Failures.Map must win.
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Failures.Map<NotFoundFailure>(f =>
                new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code)));

        var capture = await Execute(factory, factory.FromFailure(new NotFoundFailure("Missing")));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, capture.StatusCode);
    }

    [Fact]
    public async Task FromFailure_DerivedType_UsesNearestBaseTypeMapper()
    {
        // DerivedQuota inherits QuotaStub; the mapper registered for QuotaStub must apply.
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Failures.Map<QuotaStub>(f =>
                new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code)));

        var capture = await Execute(factory, factory.FromFailure(new DerivedQuota("over limit")));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("over limit", json.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task FromFailure_WithCustomTypedEnvelope_RendersIt()
    {
        // ErrorEnvelope.Set<T> changes the shape of every error response — here a typed
        // body with an errorCode — while the status still comes from the failure.
        var options = new ResultPatternOptions();
        options.ErrorEnvelope.Set<AppError>((details, ctx) => new AppError
        {
            Code = details.Code,
            Message = details.Message,
            TraceId = ctx.TraceIdentifier,
        });
        var factory = new HttpResultFactory(options);

        var capture = await Execute(factory, factory.FromFailure(new NotFoundFailure("Missing")));

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Missing", json.GetProperty("message").GetString());
        Assert.Equal("not_found", json.GetProperty("code").GetString());
        Assert.True(json.TryGetProperty("traceId", out _)); // per-request field, typed
    }

    [Fact]
    public async Task AddOutcomeHttp_ProducesCleanProblemDetails_ByDefault()
    {
        // AddOutcomeHttp alone yields RFC 7807 ProblemDetails with a clean body —
        // no traceId/path unless a typed envelope is configured.
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
    public async Task AddOutcomeHttp_WithConfiguredTypedEnvelope_RendersIt()
    {
        // Everything is configured at AddOutcomeHttp — no Replace needed to change the
        // error shape.
        var services = new ServiceCollection();
        services.AddOutcomeHttp(options =>
            options.ErrorEnvelope.Set<AppError>((details, ctx) => new AppError
            {
                Code = details.Code,
                Message = details.Message,
            }));

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpResultFactory>();

        var capture = await Execute(factory, factory.FromFailure(new NotFoundFailure("Missing")));

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Missing", json.GetProperty("message").GetString());
        Assert.Equal("not_found", json.GetProperty("code").GetString());
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

    /// <summary>A user-defined typed error envelope — inherits nothing, serializes fully.</summary>
    private sealed class AppError
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
        public string? TraceId { get; set; }
        public string? Path { get; set; }
        public IReadOnlyDictionary<string, string[]>? FieldErrors { get; set; }
    }
}
