using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Failures;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Tests.TestHelpers;

namespace NebulaSpace.Outcome.AspNetCore.Tests;

public class HttpResultTests : IDisposable
{
    public HttpResultTests() => ResultTestHelper.ResetOptions();

    public void Dispose() => ResultTestHelper.ResetOptions();

    private static async Task<ResultCapture> Execute(IResult result, string path = "/") =>
        await ResultTestHelper.ExecuteAsync(result, ctx => ctx.Request.Path = path);

    // --- Ok ---

    [Fact]
    public async Task Ok_WithEnvelope_WrapsDataInSuccessResponse()
    {
        // EnvelopeSuccess defaults to true.
        var capture = await Execute(HttpResult.Ok(new { greeting = "hi" }));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(200, json.GetProperty("statusCode").GetInt32());
        Assert.Equal("hi", json.GetProperty("data").GetProperty("greeting").GetString());
    }

    [Fact]
    public async Task Ok_WithEnvelopeDisabled_ReturnsRawData()
    {
        ResultTestHelper.ConfigureOptions(options => options.EnvelopeSuccess = false);

        var capture = await Execute(HttpResult.Ok(new { greeting = "hi" }));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("hi", json.GetProperty("greeting").GetString());
        Assert.False(json.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task Ok_WithPagination_IncludesPaginationInEnvelope()
    {
        var pagination = Pagination.Create(1, 10, 37);
        var capture = await Execute(HttpResult.Ok(new[] { 1, 2, 3 }, pagination));

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
        var order = new { Id = Guid.NewGuid() };
        var capture = await Execute(HttpResult.Created(order, o => o.Id), path: "/orders");

        Assert.Equal(StatusCodes.Status201Created, capture.StatusCode);
        Assert.Equal($"/orders/{order.Id}", capture.Location);
    }

    [Fact]
    public async Task Created_WithEnvelope_WrapsDataInSuccessResponse()
    {
        var order = new { Id = Guid.NewGuid() };
        var capture = await Execute(HttpResult.Created(order, o => o.Id), path: "/orders");

        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(201, json.GetProperty("statusCode").GetInt32());
        Assert.Equal(order.Id.ToString(), json.GetProperty("data").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Created_WithEnvelopeDisabled_ReturnsRawData()
    {
        ResultTestHelper.ConfigureOptions(options => options.EnvelopeSuccess = false);
        var order = new { Id = Guid.NewGuid() };

        var capture = await Execute(HttpResult.Created(order, o => o.Id), path: "/orders");

        Assert.Equal(StatusCodes.Status201Created, capture.StatusCode);
        Assert.Equal($"/orders/{order.Id}", capture.Location);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(order.Id.ToString(), json.GetProperty("id").GetString());
        Assert.False(json.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task Created_TrimsTrailingSlashOnPath()
    {
        var order = new { Id = 5 };
        var capture = await Execute(HttpResult.Created(order, o => o.Id), path: "/orders/");

        Assert.Equal("/orders/5", capture.Location);
    }

    // --- NoContent ---

    [Fact]
    public async Task NoContent_Returns204WithEmptyBody()
    {
        var capture = await Execute(HttpResult.NoContent());

        Assert.Equal(StatusCodes.Status204NoContent, capture.StatusCode);
        Assert.Equal("", capture.Body);
    }

    // --- 4xx ---

    [Fact]
    public async Task BadRequest_Sets400AndMessage()
    {
        var capture = await Execute(HttpResult.BadRequest("oops"));

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("oops", json.GetProperty("message").GetString());
        Assert.Equal(400, json.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Unauthorized_Sets401()
    {
        var capture = await Execute(HttpResult.Unauthorized());

        Assert.Equal(StatusCodes.Status401Unauthorized, capture.StatusCode);
    }

    [Fact]
    public async Task Forbidden_Sets403()
    {
        var capture = await Execute(HttpResult.Forbidden());

        Assert.Equal(StatusCodes.Status403Forbidden, capture.StatusCode);
    }

    [Fact]
    public async Task NotFound_Sets404()
    {
        var capture = await Execute(HttpResult.NotFound());

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(404, json.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Conflict_Sets409()
    {
        var capture = await Execute(HttpResult.Conflict());

        Assert.Equal(StatusCodes.Status409Conflict, capture.StatusCode);
    }

    // --- FromFailure ---

    [Fact]
    public async Task FromFailure_NotFound_MapsTo404()
    {
        var capture = await Execute(HttpResult.FromFailure(new NotFoundFailure("Missing")));

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Missing", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task FromFailure_Validation_MapsTo400WithErrors()
    {
        var errors = new Dictionary<string, string> { ["Amount"] = "too small" };
        var capture = await Execute(HttpResult.FromFailure(new ValidationFailure("Invalid", errors)));

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("too small", json.GetProperty("errors").GetProperty("Amount").GetString());
    }

    [Fact]
    public async Task FromFailure_BadRequest_MapsTo400()
    {
        var capture = await Execute(HttpResult.FromFailure(new BadRequestFailure("Bad")));

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
    }

    [Fact]
    public async Task FromFailure_Conflict_MapsTo409()
    {
        var capture = await Execute(HttpResult.FromFailure(new ConflictFailure("Conflict")));

        Assert.Equal(StatusCodes.Status409Conflict, capture.StatusCode);
    }

    [Fact]
    public async Task FromFailure_Unauthorized_MapsTo401()
    {
        var capture = await Execute(HttpResult.FromFailure(new UnauthorizedFailure("Auth")));

        Assert.Equal(StatusCodes.Status401Unauthorized, capture.StatusCode);
    }

    [Fact]
    public async Task FromFailure_Forbidden_MapsTo403()
    {
        var capture = await Execute(HttpResult.FromFailure(new ForbiddenFailure("Denied")));

        Assert.Equal(StatusCodes.Status403Forbidden, capture.StatusCode);
    }

    [Fact]
    public void FromFailure_UnknownFailure_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HttpResult.FromFailure(new UnknownFailureStub()));
    }

    // --- Custom MapFailure ---

    [Fact]
    public async Task FromFailure_CustomMappedFailure_UsesCustomResult()
    {
        ResultTestHelper.ConfigureOptions(options =>
            options.MapFailure<QuotaStub>(f =>
                Results.Json(
                    new { message = f.Message },
                    statusCode: StatusCodes.Status422UnprocessableEntity)));

        var capture = await Execute(HttpResult.FromFailure(new QuotaStub("over limit")));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("over limit", json.GetProperty("message").GetString());
    }

    private sealed class UnknownFailureStub : Failure
    {
    }

    private sealed class QuotaStub : Failure
    {
        public QuotaStub(string message) => Message = message;
        public string Message { get; }
    }
}
