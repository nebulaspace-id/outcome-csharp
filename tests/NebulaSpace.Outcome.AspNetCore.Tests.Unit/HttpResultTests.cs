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
    public async Task Ok_NoArgs_ReturnsDefaultSuccessResponse()
    {
        var factory = ResultTestHelper.CreateFactory();

        var capture = await Execute(factory, factory.Ok());

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(200, json.GetProperty("statusCode").GetInt32());
        Assert.False(json.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task Ok_NoArgs_WithCustomEnvelope_UsesCustomEnvelope()
    {
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Success.SetResponse(typeof(DefaultSuccessResponse<>)));

        var capture = await Execute(factory, factory.Ok());

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(200, json.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Ok_WithEnvelope_WrapsDataInSuccessResponse()
    {
        var factory = ResultTestHelper.CreateFactory();

        // Success.Enabled defaults to true.
        var capture = await Execute(factory, factory.Ok(new { greeting = "hi" }));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(200, json.GetProperty("statusCode").GetInt32());
        Assert.Equal("hi", json.GetProperty("data").GetProperty("greeting").GetString());
    }

    [Fact]
    public async Task Ok_WithEnvelopeDisabled_ReturnsRawData()
    {
        var factory = ResultTestHelper.CreateFactory(options => options.Success.Enabled = false);

        var capture = await Execute(factory, factory.Ok(new { greeting = "hi" }));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("hi", json.GetProperty("greeting").GetString());
        Assert.False(json.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task Ok_WithPagination_IncludesFullConcretePaginationInEnvelope()
    {
        // 2‑param envelope so STJ serializes the full DefaultOffsetInfo shape,
        // not just the IPaginationDetails interface members.
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Success.SetResponse(typeof(DefaultSuccessResponse<,>)));

        var pagination = Pagination.DefaultOffsetInfo.Create(1, 10, 37);
        var capture = await Execute(factory, factory.Ok(new[] { 1, 2, 3 }, pagination));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        var page = json.GetProperty("pagination");
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(10, page.GetProperty("limit").GetInt32());
        Assert.Equal(4, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(37, page.GetProperty("totalItems").GetInt32());
        Assert.True(page.GetProperty("hasMore").GetBoolean());
    }

    [Fact]
    public async Task Ok_WithCursorPagination_IncludesFullCursorInfoInEnvelope()
    {
        // 2‑param envelope closes TPage with DefaultCursorInfo, so STJ sees
        // NextCursor, PreviousCursor, HasMore, TotalItems — not just IPaginationDetails.
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Success.SetResponse(typeof(DefaultSuccessResponse<,>)));

        var cursorInfo = new Pagination.DefaultCursorInfo
        {
            PreviousCursor = "prev-abc",
            NextCursor = "next-def",
            HasMore = true,
            TotalItems = 100,
        };

        var capture = await Execute(factory, factory.Ok(new[] { 1, 2, 3 }, cursorInfo));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        var page = json.GetProperty("pagination");
        Assert.Equal("prev-abc", page.GetProperty("previousCursor").GetString());
        Assert.Equal("next-def", page.GetProperty("nextCursor").GetString());
        Assert.True(page.GetProperty("hasMore").GetBoolean());
        Assert.Equal(100, page.GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task Ok_WithCustomPaginationDetails_UsesConcreteType()
    {
        // Custom IPaginationDetails type — the factory resolves TPage at runtime
        // via pagination?.GetType(), so STJ serializes the full shape.
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Success.SetResponse(typeof(DefaultSuccessResponse<,>)));

        var keyset = new CustomKeysetPage
        {
            NextKey = "abc-123",
            PreviousKey = null,
            HasMore = true,
            TotalItems = 500,
        };

        var capture = await Execute(factory, factory.Ok(new[] { 1, 2, 3 }, keyset));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        var page = json.GetProperty("pagination");
        Assert.Equal("abc-123", page.GetProperty("nextKey").GetString());
        Assert.True(page.GetProperty("hasMore").GetBoolean());
        Assert.Equal(500, page.GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task Ok_WithPagination_NoTwoParamEnvelopeRegistered_FallsBackToDefault()
    {
        // No envelope registered at all — falls back to DefaultSuccessResponse.
        // Pagination must still appear via the auto-fallback
        // DefaultSuccessResponse<TData, TPage>, never silently dropped.
        var factory = ResultTestHelper.CreateFactory(); // no SetResponse call

        var pagination = Pagination.DefaultOffsetInfo.Create(2, 5, 42);
        var capture = await Execute(factory, factory.Ok(new[] { "a", "b" }, pagination));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.True(json.TryGetProperty("pagination", out _), "pagination must not be dropped");
        var page = json.GetProperty("pagination");
        Assert.Equal(2, page.GetProperty("page").GetInt32());
        Assert.Equal(5, page.GetProperty("limit").GetInt32());
        Assert.Equal(9, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(42, page.GetProperty("totalItems").GetInt32());
    }

    private sealed class CustomKeysetPage : Pagination.IPaginationDetails
    {
        public string? NextKey { get; set; }
        public string? PreviousKey { get; set; }
        public bool HasMore { get; set; }
        public long? TotalItems { get; set; }
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
        var factory = ResultTestHelper.CreateFactory(options => options.Success.Enabled = false);
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

    [Fact]
    public async Task Created_WithManualUri_UsesProvidedLocation()
    {
        var factory = ResultTestHelper.CreateFactory();
        var order = new { Id = Guid.NewGuid() };
        var capture = await Execute(factory, factory.Created(order, "/custom/orders/42"), path: "/orders");

        Assert.Equal(StatusCodes.Status201Created, capture.StatusCode);
        Assert.Equal("/custom/orders/42", capture.Location);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(201, json.GetProperty("statusCode").GetInt32());
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
        // factory.NotFound() has no message -> Detail is "" (DefaultErrorResponse is
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
        // The real AddOutcome flow: a typed envelope configured via Error.SetResponse(Type).
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Error.SetResponse<AppError>());

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
            options.Error.SetResponse<AppError>());

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
            options.Error.MapFailure<QuotaStub>(f =>
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
        // Section-based API: options.Error.MapFailure<T>(...).
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Error.MapFailure<QuotaStub>(f =>
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
            options.Error.MapFailure<NotFoundFailure>(f =>
                new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code)));

        var capture = await Execute(factory, factory.FromFailure(new NotFoundFailure("Missing")));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, capture.StatusCode);
    }

    [Fact]
    public async Task FromFailure_DerivedType_UsesNearestBaseTypeMapper()
    {
        // DerivedQuota inherits QuotaStub; the mapper registered for QuotaStub must apply.
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Error.MapFailure<QuotaStub>(f =>
                new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code)));

        var capture = await Execute(factory, factory.FromFailure(new DerivedQuota("over limit")));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("over limit", json.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task FromFailure_WithCustomTypedEnvelope_RendersIt()
    {
        // Error.SetResponse<T> changes the shape of every error response — here a typed
        // body with an errorCode — while the status still comes from the failure.
        var options = new OutcomeOptions();
        options.Error.SetResponse<AppError>();
        var factory = new HttpResultFactory(options);

        var capture = await Execute(factory, factory.FromFailure(new NotFoundFailure("Missing")));

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Missing", json.GetProperty("message").GetString());
        Assert.Equal("not_found", json.GetProperty("code").GetString());
        Assert.True(json.TryGetProperty("traceId", out _)); // per-request field, typed
    }

    [Fact]
    public async Task AddOutcome_ProducesCleanProblemDetails_ByDefault()
    {
        // AddOutcome alone yields RFC 7807 ProblemDetails with a clean body —
        // no traceId/path unless a typed envelope is configured.
        var services = new ServiceCollection();
        services.AddOutcome();

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
    public async Task AddOutcome_WithConfiguredTypedEnvelope_RendersIt()
    {
        // Everything is configured at AddOutcome — no Replace needed to change the
        // error shape.
        var services = new ServiceCollection();
        services.AddOutcome(options =>
            options.Error.SetResponse<AppError>());

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpResultFactory>();

        var capture = await Execute(factory, factory.FromFailure(new NotFoundFailure("Missing")));

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Missing", json.GetProperty("message").GetString());
        Assert.Equal("not_found", json.GetProperty("code").GetString());
    }

    [Fact]
    public void AddOutcome_ThrowsOnDoubleRegistration()
    {
        var services = new ServiceCollection();
        services.AddOutcome();

        Assert.Throws<InvalidOperationException>(() => services.AddOutcome());
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
    private sealed class AppError : IErrorEnvelope
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
        public string? TraceId { get; set; }
        public string? Path { get; set; }
        public IReadOnlyDictionary<string, string[]>? FieldErrors { get; set; }

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
