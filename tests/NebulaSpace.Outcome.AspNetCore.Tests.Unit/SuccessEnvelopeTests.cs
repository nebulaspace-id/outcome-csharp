using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Pagination;
using NebulaSpace.Outcome.AspNetCore.Tests.Unit.TestHelpers;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

/// <summary>
/// Verifies the typed success envelope: <see cref="SuccessEnvelopeResult{T}"/> renders
/// the user-defined envelope type via <c>options.SuccessEnvelope.Set(typeof(AppSuccess&lt;&gt;))</c>
/// (built from the payload, pagination, request, and status), and falls back to the
/// default <see cref="DefaultSuccessResponse{T}"/> when no envelope is configured.
/// A custom envelope must implement <see cref="ISuccessEnvelope{T}"/>.
/// </summary>
public class SuccessEnvelopeTests
{
    [Fact]
    public async Task Ok_Default_SerializesDefaultSuccessResponse()
    {
        var factory = ResultTestHelper.CreateFactory();
        var capture = await ResultTestHelper.ExecuteAsync(factory.Ok(new { greeting = "halo" }));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        var root = doc.RootElement;
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
        Assert.Equal("halo", root.GetProperty("data").GetProperty("greeting").GetString());
    }

    [Fact]
    public async Task Ok_WithTypedEnvelope_SerializesAsUserType_WithPerRequestFields()
    {
        var factory = ResultTestHelper.CreateFactory(options =>
            options.SuccessEnvelope.Set(typeof(AppSuccess<>)));

        var capture = await ResultTestHelper.ExecuteAsync(factory.Ok(new { greeting = "halo" }));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        var root = doc.RootElement;
        Assert.Equal("halo", root.GetProperty("data").GetProperty("greeting").GetString());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
        Assert.True(root.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Ok_WithTypedEnvelope_ReceivesStatusCode()
    {
        var factory = ResultTestHelper.CreateFactory(options =>
            options.SuccessEnvelope.Set(typeof(AppSuccess<>)));

        var capture = await ResultTestHelper.ExecuteAsync(factory.Ok(new { greeting = "halo" }));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        Assert.Equal(200, doc.RootElement.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Created_WithTypedEnvelope_ReceivesCreatedStatusCode()
    {
        var factory = ResultTestHelper.CreateFactory(options =>
            options.SuccessEnvelope.Set(typeof(AppSuccess<>)));

        var order = new { Id = Guid.NewGuid() };
        var capture = await ResultTestHelper.ExecuteAsync(
            factory.Created(order, o => o.Id), ctx => ctx.Request.Path = "/orders");

        Assert.Equal(StatusCodes.Status201Created, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        Assert.Equal(201, doc.RootElement.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Ok_WithTypedEnvelope_AndPagination_ForwardsPagination()
    {
        var factory = ResultTestHelper.CreateFactory(options =>
            options.SuccessEnvelope.Set(typeof(AppSuccess<>)));

        var pagination = PageInfo.Create(page: 2, pageSize: 10, totalItems: 37);
        var capture = await ResultTestHelper.ExecuteAsync(factory.Ok(new[] { 1, 2 }, pagination));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        var root = doc.RootElement;
        Assert.Equal(2, root.GetProperty("pagination").GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("pagination").GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Ok_Disabled_ReturnsRawData()
    {
        var factory = ResultTestHelper.CreateFactory(options =>
        {
            options.SuccessEnvelope.Enabled = false;
            options.SuccessEnvelope.Set(typeof(AppSuccess<>));
        });

        var capture = await ResultTestHelper.ExecuteAsync(factory.Ok(new { greeting = "halo" }));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        Assert.Equal("halo", doc.RootElement.GetProperty("greeting").GetString());
        Assert.False(doc.RootElement.TryGetProperty("data", out _));
    }

    [Fact]
    public void Set_EnvelopeNotImplementingInterface_ThrowsInvalidOperationException()
    {
        var options = new ResultPatternOptions();

        var ex = Assert.Throws<InvalidOperationException>(() => options.SuccessEnvelope.Set(typeof(NoFromEnvelope)));
        Assert.Contains("ISuccessEnvelope", ex.Message);
    }

    /// <summary>A user-defined typed success envelope — serializes fully, built via ISuccessEnvelope.From.</summary>
    private sealed class AppSuccess<T> : ISuccessEnvelope<T>
    {
        public string? TraceId { get; set; }
        public int StatusCode { get; set; }
        public T? Data { get; set; }
        public PageInfo? Pagination { get; set; }

        public static ISuccessEnvelope<T> From(T data, PageInfo? pagination, HttpContext httpContext, int statusCode) =>
            new AppSuccess<T>
            {
                TraceId = httpContext.TraceIdentifier,
                StatusCode = statusCode,
                Data = data,
                Pagination = pagination,
            };
    }

    /// <summary>An envelope type that forgets to implement <c>ISuccessEnvelope{T}</c> — Set must fail fast.</summary>
    private sealed class NoFromEnvelope
    {
        public object? Data { get; set; }
    }
}
