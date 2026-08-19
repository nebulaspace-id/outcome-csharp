using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Failures;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Tests.Unit.TestHelpers;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class OutcomeHttpExtensionsTests
{
    [Fact]
    public async Task ToResult_OnSuccess_InvokesOnSuccess()
    {
        var factory = ResultTestHelper.CreateFactory();
        Outcome<string> outcome = Outcome.Success("hi");

        var capture = await ResultTestHelper.ExecuteAsync(
            outcome.ToResult(factory, value => factory.Ok(new { value })));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("hi", json.GetProperty("data").GetProperty("value").GetString());
    }

    [Fact]
    public async Task ToResult_OnFailure_ReturnsErrorResponse()
    {
        var factory = ResultTestHelper.CreateFactory();
        Outcome<Order> outcome = Outcome.Fail(new NotFoundFailure("not here"));

        var capture = await ResultTestHelper.ExecuteAsync(
            outcome.ToResult(factory, order => factory.Ok(order)));

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Not Found", json.GetProperty("title").GetString());
        Assert.Equal("not here", json.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ToResult_OnFailure_DoesNotCallOnSuccess()
    {
        var factory = ResultTestHelper.CreateFactory();
        Outcome<int> outcome = Outcome.Fail(new BadRequestFailure("bad"));
        var onSuccessCalled = false;

        await ResultTestHelper.ExecuteAsync(
            outcome.ToResult(factory, _ =>
            {
                onSuccessCalled = true;
                return factory.Ok<object>(new object());
            }));

        Assert.False(onSuccessCalled);
    }

    [Fact]
    public async Task ToErrorResponse_ReturnsFromFailure()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await ResultTestHelper.ExecuteAsync(
            new ForbiddenFailure("nope").ToErrorResponse(factory));

        Assert.Equal(StatusCodes.Status403Forbidden, capture.StatusCode);
    }

    [Fact]
    public async Task ToResult_SuccessWithCreated_ProducesLocation()
    {
        var factory = ResultTestHelper.CreateFactory();
        Outcome<Order> outcome = Outcome.Success(new Order(1));
        var capture = await ResultTestHelper.ExecuteAsync(
            outcome.ToResult(factory, order => factory.Created(order, o => o.Id)),
            ctx => ctx.Request.Path = "/orders");

        Assert.Equal(StatusCodes.Status201Created, capture.StatusCode);
        Assert.Equal("/orders/1", capture.Location);
    }

    // --- ToResultAsync ---

    [Fact]
    public async Task ToResultAsync_OnSuccess_InvokesOnSuccess()
    {
        var factory = ResultTestHelper.CreateFactory();
        Task<Outcome<string>> outcomeTask = Task.FromResult(Outcome.Success("hi"));

        var capture = await ResultTestHelper.ExecuteAsync(
            await outcomeTask.ToResultAsync(factory, value => factory.Ok(new { value })));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("hi", json.GetProperty("data").GetProperty("value").GetString());
    }

    [Fact]
    public async Task ToResultAsync_OnFailure_ReturnsErrorResponse()
    {
        var factory = ResultTestHelper.CreateFactory();
        Task<Outcome<Order>> outcomeTask = Task.FromResult<Outcome<Order>>(Outcome.Fail(new NotFoundFailure("nope")));

        var capture = await ResultTestHelper.ExecuteAsync(
            await outcomeTask.ToResultAsync(factory, order => factory.Ok(order)));

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("nope", json.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ToResultAsync_WithAsyncOnSuccess_IsAwaited()
    {
        var factory = ResultTestHelper.CreateFactory();
        Task<Outcome<string>> outcomeTask = Task.FromResult(Outcome.Success("hi"));

        var capture = await ResultTestHelper.ExecuteAsync(
            await outcomeTask.ToResultAsync(factory, async value =>
            {
                await Task.Yield();
                return factory.Ok(new { value });
            }));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("hi", json.GetProperty("data").GetProperty("value").GetString());
    }

    private sealed record Order(int Id);
}
