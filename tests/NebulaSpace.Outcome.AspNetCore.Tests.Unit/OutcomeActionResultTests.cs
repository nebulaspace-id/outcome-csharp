using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Failures;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Tests.Unit.TestHelpers;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class OutcomeActionResultTests
{
    [Fact]
    public async Task Success_Outcome_Produces200WithEnvelope()
    {
        var factory = ResultTestHelper.CreateFactory();
        Outcome<string> outcome = Outcome.Success("hello");
        var actionResult = new OutcomeActionResult(factory.Ok("hello"));

        var capture = await ResultTestHelper.ExecuteActionResultAsync(actionResult);

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("hello", json.GetProperty("data").GetString());
    }

    [Fact]
    public async Task Failure_Outcome_ProducesMappedError()
    {
        var factory = ResultTestHelper.CreateFactory();
        var actionResult = new OutcomeActionResult(factory.FromFailure(new NotFoundFailure("not here")));

        var capture = await ResultTestHelper.ExecuteActionResultAsync(actionResult);

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Not Found", json.GetProperty("title").GetString());
        Assert.Equal("not here", json.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Created_Result_Produces201WithLocation()
    {
        var factory = ResultTestHelper.CreateFactory();
        var inner = factory.Created("/orders/42", new Order(42));
        var actionResult = new OutcomeActionResult(inner);

        var capture = await ResultTestHelper.ExecuteActionResultAsync(
            actionResult,
            ctx => ctx.Request.Path = "/orders");

        Assert.Equal(StatusCodes.Status201Created, capture.StatusCode);
        Assert.Equal("/orders/42", capture.Location);
    }

    [Fact]
    public async Task NoContent_Result_Produces204()
    {
        var factory = ResultTestHelper.CreateFactory();
        var actionResult = new OutcomeActionResult(factory.NoContent());

        var capture = await ResultTestHelper.ExecuteActionResultAsync(actionResult);

        Assert.Equal(StatusCodes.Status204NoContent, capture.StatusCode);
        Assert.Equal("", capture.Body);
    }

    [Fact]
    public async Task BadRequest_Failure_Produces400()
    {
        var factory = ResultTestHelper.CreateFactory();
        var actionResult = new OutcomeActionResult(factory.FromFailure(new BadRequestFailure("bad input")));

        var capture = await ResultTestHelper.ExecuteActionResultAsync(actionResult);

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("bad input", json.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ValidationFailure_Produces400WithErrors()
    {
        var factory = ResultTestHelper.CreateFactory();
        var errors = new Dictionary<string, string[]>
        {
            ["Name"] = ["Name is required"],
        };
        var actionResult = new OutcomeActionResult(factory.FromFailure(
            new ValidationFailure("Validation failed", errors)));

        var capture = await ResultTestHelper.ExecuteActionResultAsync(actionResult);

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("Validation failed", json.GetProperty("detail").GetString());
    }

    [Fact]
    public void Inner_Property_ReturnsWrappedResult()
    {
        var factory = ResultTestHelper.CreateFactory();
        var inner = factory.Ok("test");
        var actionResult = new OutcomeActionResult(inner);

        Assert.Same(inner, actionResult.Inner);
    }

    [Fact]
    public void Constructor_NullResult_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OutcomeActionResult(null!));
    }

    private sealed record Order(int Id);
}
