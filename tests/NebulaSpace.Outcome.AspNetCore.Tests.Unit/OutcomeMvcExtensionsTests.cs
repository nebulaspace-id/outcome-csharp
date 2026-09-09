using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NebulaSpace.Outcome.AspNetCore.Extensions;
using NebulaSpace.Outcome.AspNetCore.Failures;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Mvc;
using NebulaSpace.Outcome.AspNetCore.Tests.Unit.TestHelpers;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class OutcomeMvcExtensionsTests
{
    // ──────────────────────────────────────────────────────────────────────
    //  ToActionResult (no onSuccess)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ToActionResult_OnSuccess_Produces200()
    {
        var factory = ResultTestHelper.CreateFactory();
        Outcome<string> outcome = Outcome.Success("hi");

        var capture = await ResultTestHelper.ExecuteActionResultAsync(
            outcome.ToActionResult(factory));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("hi", json.GetProperty("data").GetString());
    }

    [Fact]
    public async Task ToActionResult_OnFailure_ProducesMappedError()
    {
        var factory = ResultTestHelper.CreateFactory();
        Outcome<string> outcome = Outcome.Fail(new NotFoundFailure("missing"));

        var capture = await ResultTestHelper.ExecuteActionResultAsync(
            outcome.ToActionResult(factory));

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("missing", json.GetProperty("detail").GetString());
    }

    // ──────────────────────────────────────────────────────────────────────
    //  ToActionResultAsync (sync onSuccess)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ToActionResultAsync_OnSuccess_Produces200()
    {
        var factory = ResultTestHelper.CreateFactory();
        Task<Outcome<string>> outcomeTask = Task.FromResult(Outcome.Success("async"));

        var capture = await ResultTestHelper.ExecuteActionResultAsync(
            await outcomeTask.ToActionResultAsync(factory));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("async", json.GetProperty("data").GetString());
    }

    [Fact]
    public async Task ToActionResultAsync_OnFailure_ProducesMappedError()
    {
        var factory = ResultTestHelper.CreateFactory();
        Task<Outcome<string>> outcomeTask = Task.FromResult<Outcome<string>>(
            Outcome.Fail(new BadRequestFailure("bad")));

        var capture = await ResultTestHelper.ExecuteActionResultAsync(
            await outcomeTask.ToActionResultAsync(factory));

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  ToActionResultAsync (async onSuccess)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ToActionResultAsync_AsyncHandler_IsAwaited()
    {
        var factory = ResultTestHelper.CreateFactory();
        Task<Outcome<string>> outcomeTask = Task.FromResult(Outcome.Success("awaited"));

        var capture = await ResultTestHelper.ExecuteActionResultAsync(
            await outcomeTask.ToActionResultAsync(factory, async value =>
            {
                await Task.Yield();
                return factory.Ok(new { value });
            }));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("awaited", json.GetProperty("data").GetProperty("value").GetString());
    }

    // ──────────────────────────────────────────────────────────────────────
    //  ToActionResult with onSuccess (existing in OutcomeHttpExtensions)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ToActionResult_WithOnSuccess_InvokesOnSuccess()
    {
        var factory = ResultTestHelper.CreateFactory();
        Outcome<string> outcome = Outcome.Success("custom");

        var capture = await ResultTestHelper.ExecuteActionResultAsync(
            outcome.ToActionResult(factory, value => factory.Ok(new { value })));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("custom", json.GetProperty("data").GetProperty("value").GetString());
    }

    [Fact]
    public async Task ToActionResult_WithOnSuccess_OnFailure_DoesNotCallOnSuccess()
    {
        var factory = ResultTestHelper.CreateFactory();
        Outcome<int> outcome = Outcome.Fail(new BadRequestFailure("bad"));
        var onSuccessCalled = false;

        await ResultTestHelper.ExecuteActionResultAsync(
            outcome.ToActionResult(factory, _ =>
            {
                onSuccessCalled = true;
                return factory.Ok<object>(new object());
            }));

        Assert.False(onSuccessCalled);
    }

    [Fact]
    public async Task ToActionResult_WithOnSuccess_CreatedProducesLocation()
    {
        var factory = ResultTestHelper.CreateFactory();
        Outcome<Order> outcome = Outcome.Success(new Order(7));

        var capture = await ResultTestHelper.ExecuteActionResultAsync(
            outcome.ToActionResult(factory, order => factory.Created($"/orders/{order.Id}", order)),
            ctx => ctx.Request.Path = "/orders");

        Assert.Equal(StatusCodes.Status201Created, capture.StatusCode);
        Assert.Equal("/orders/7", capture.Location);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  OutcomeResultAttribute (filter)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Filter_ConvertsOutcomeInObjectResult()
    {
        var factory = ResultTestHelper.CreateFactory();
        Outcome<string> outcome = Outcome.Success("filtered");

        // Simulate MVC pipeline: ObjectResult wrapping an Outcome<T>
        var context = CreateResultExecutingContext(outcome);
        var executed = false;
        ResultExecutionDelegate next = () =>
        {
            executed = true;
            return Task.FromResult(new ResultExecutedContext(context, [], context.Result, null!));
        };

        var filter = new OutcomeResultAttribute();
        await filter.OnResultExecutionAsync(context, next);

        Assert.True(executed);
        Assert.IsType<OutcomeActionResult>(context.Result);

        // Execute the ActionResult to verify it produces the right response
        var capture = await ResultTestHelper.ExecuteActionResultAsync(
            (IActionResult)context.Result);

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal("filtered", json.GetProperty("data").GetString());
    }

    [Fact]
    public async Task Filter_ConvertsFailureOutcome()
    {
        Outcome<string> outcome = Outcome.Fail(new NotFoundFailure("gone"));

        var context = CreateResultExecutingContext(outcome);
        var executed = false;
        ResultExecutionDelegate next = () =>
        {
            executed = true;
            return Task.FromResult(new ResultExecutedContext(context, [], context.Result, null!));
        };

        var filter = new OutcomeResultAttribute();
        await filter.OnResultExecutionAsync(context, next);

        Assert.True(executed);
        Assert.IsType<OutcomeActionResult>(context.Result);

        var capture = await ResultTestHelper.ExecuteActionResultAsync(
            (IActionResult)context.Result);

        Assert.Equal(StatusCodes.Status404NotFound, capture.StatusCode);
    }

    [Fact]
    public async Task Filter_ConvertsValueTypeOutcome()
    {
        // Boxing edge: payload is a struct, so Value round-trips through object.
        Outcome<int> outcome = Outcome.Success(42);

        var context = CreateResultExecutingContext(outcome);
        ResultExecutionDelegate next = () => Task.FromResult(
            new ResultExecutedContext(context, [], context.Result, null!));

        var filter = new OutcomeResultAttribute();
        await filter.OnResultExecutionAsync(context, next);

        var capture = await ResultTestHelper.ExecuteActionResultAsync(
            (IActionResult)context.Result);

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        var json = ResultTestHelper.ParseJson(capture.Body);
        Assert.Equal(42, json.GetProperty("data").GetInt32());
    }

    [Fact]
    public async Task Filter_PassesThroughNonOutcomeValues()
    {
        var context = CreateResultExecutingContext("plain string");
        var originalResult = context.Result;
        var executed = false;
        ResultExecutionDelegate next = () =>
        {
            executed = true;
            return Task.FromResult(new ResultExecutedContext(context, [], context.Result, null!));
        };

        var filter = new OutcomeResultAttribute();
        await filter.OnResultExecutionAsync(context, next);

        Assert.True(executed);
        Assert.Same(originalResult, context.Result); // untouched
    }

    private static Microsoft.AspNetCore.Mvc.Filters.ResultExecutingContext CreateResultExecutingContext(object value)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
                .AddLogging()
                .AddOutcome()
                .BuildServiceProvider(),
        };
        httpContext.Response.Body = new MemoryStream();

        return new Microsoft.AspNetCore.Mvc.Filters.ResultExecutingContext(
            new Microsoft.AspNetCore.Mvc.ActionContext
            {
                HttpContext = httpContext,
                RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
                ActionDescriptor = new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor(),
            },
            [],
            new Microsoft.AspNetCore.Mvc.ObjectResult(value),
            controller: null!);
    }

    private sealed record Order(int Id);
}
