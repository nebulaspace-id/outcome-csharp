using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Failures;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Tests.TestHelpers;

namespace NebulaSpace.Outcome.AspNetCore.Tests;

public class OutcomeHttpExtensionsTests : IDisposable
{
    public OutcomeHttpExtensionsTests() => ResultTestHelper.ResetOptions();

    public void Dispose() => ResultTestHelper.ResetOptions();

    [Fact]
    public async Task ToHttpResult_OnSuccess_InvokesOnSuccess()
    {
        Outcome<string> outcome = Outcome.Success("hi");

        var capture = await ResultTestHelper.ExecuteAsync(
            outcome.ToHttpResult(value => HttpResult.Ok(new { value })));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("hi", json.GetProperty("data").GetProperty("value").GetString());
    }

    [Fact]
    public async Task ToHttpResult_OnFailure_ReturnsErrorResponse()
    {
        Outcome<Order> outcome = Outcome.Fail(new NotFoundFailure("not here"));

        var capture = await ResultTestHelper.ExecuteAsync(
            outcome.ToHttpResult(order => HttpResult.Ok(order)));

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("not here", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ToHttpResult_OnFailure_DoesNotCallOnSuccess()
    {
        Outcome<int> outcome = Outcome.Fail(new BadRequestFailure("bad"));
        var onSuccessCalled = false;

        await ResultTestHelper.ExecuteAsync(
            outcome.ToHttpResult(_ =>
            {
                onSuccessCalled = true;
                return HttpResult.Ok<object>(new object());
            }));

        Assert.False(onSuccessCalled);
    }

    [Fact]
    public async Task ToErrorResponse_ReturnsFromFailure()
    {
        var capture = await ResultTestHelper.ExecuteAsync(
            new ForbiddenFailure("nope").ToErrorResponse());

        Assert.Equal(StatusCodes.Status403Forbidden, capture.StatusCode);
    }

    [Fact]
    public async Task ToHttpResult_SuccessWithCreated_ProducesLocation()
    {
        Outcome<Order> outcome = Outcome.Success(new Order(1));
        var capture = await ResultTestHelper.ExecuteAsync(
            outcome.ToHttpResult(order => HttpResult.Created(order, o => o.Id)),
            ctx => ctx.Request.Path = "/orders");

        Assert.Equal(StatusCodes.Status201Created, capture.StatusCode);
        Assert.Equal("/orders/1", capture.Location);
    }

    private sealed record Order(int Id);
}
