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
/// the user-defined envelope type via <c>options.Success.SetResponse(typeof(AppSuccess&lt;&gt;))</c>
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
            options.Success.SetResponse(typeof(AppSuccess<>)));

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
            options.Success.SetResponse(typeof(AppSuccess<>)));

        var capture = await ResultTestHelper.ExecuteAsync(factory.Ok(new { greeting = "halo" }));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        Assert.Equal(200, doc.RootElement.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Created_WithTypedEnvelope_ReceivesCreatedStatusCode()
    {
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Success.SetResponse(typeof(AppSuccess<>)));

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
        // 2‑param envelope so STJ serializes the full DefaultOffsetInfo shape.
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Success.SetResponse(typeof(AppSuccess<,>)));

        var pagination = DefaultOffsetInfo.Create(page: 2, limit: 10, totalItems: 37);
        var capture = await ResultTestHelper.ExecuteAsync(factory.Ok(new[] { 1, 2 }, pagination));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        var root = doc.RootElement;
        var page = root.GetProperty("pagination");
        Assert.Equal(2, page.GetProperty("page").GetInt32());
        Assert.Equal(10, page.GetProperty("limit").GetInt32());
        Assert.Equal(4, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(37, page.GetProperty("totalItems").GetInt32());
        Assert.True(page.GetProperty("hasMore").GetBoolean());
    }

    [Fact]
    public async Task Ok_WithTypedEnvelope_AndCursorPagination_ForwardsFullCursorInfo()
    {
        // 2‑param envelope — TPage is DefaultCursorInfo, full shape serialized.
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Success.SetResponse(typeof(AppSuccess<,>)));

        var cursorInfo = new DefaultCursorInfo
        {
            PreviousCursor = "prev-abc",
            NextCursor = "next-def",
            HasMore = true,
            TotalItems = 100,
        };

        var capture = await ResultTestHelper.ExecuteAsync(factory.Ok(new[] { 1, 2 }, cursorInfo));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        var root = doc.RootElement;
        var page = root.GetProperty("pagination");
        Assert.Equal("prev-abc", page.GetProperty("previousCursor").GetString());
        Assert.Equal("next-def", page.GetProperty("nextCursor").GetString());
        Assert.True(page.GetProperty("hasMore").GetBoolean());
        Assert.Equal(100, page.GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task Ok_Disabled_ReturnsRawData()
    {
        var factory = ResultTestHelper.CreateFactory(options =>
        {
            options.Success.Enabled = false;
            options.Success.SetResponse(typeof(AppSuccess<>));
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
        var options = new OutcomeOptions();

        var ex = Assert.Throws<InvalidOperationException>(() => options.Success.SetResponse(typeof(NoFromEnvelope)));
        Assert.Contains("From", ex.Message);
    }

    [Fact]
    public async Task GetDelegateCache_AmortizesReflectionCost_PerPayloadType()
    {
        // Each distinct payload type resolves From once; subsequent requests for the same
        // type reuse the cached delegate (no GetMethod/Invoke per request).
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Success.SetResponse(typeof(AppSuccess<>)));

        // First call — fills the cache for this payload type.
        var capture1 = await ResultTestHelper.ExecuteAsync(factory.Ok(new { name = "first" }));
        Assert.Equal(StatusCodes.Status200OK, capture1.StatusCode);
        using var doc1 = JsonDocument.Parse(capture1.Body);
        Assert.Equal("first", doc1.RootElement.GetProperty("data").GetProperty("name").GetString());

        // Second call — same payload type, should use cached delegate.
        var capture2 = await ResultTestHelper.ExecuteAsync(factory.Ok(new { name = "second" }));
        Assert.Equal(StatusCodes.Status200OK, capture2.StatusCode);
        using var doc2 = JsonDocument.Parse(capture2.Body);
        Assert.Equal("second", doc2.RootElement.GetProperty("data").GetProperty("name").GetString());

        // Different payload type resolves once then is cached too.
        var capture3 = await ResultTestHelper.ExecuteAsync(factory.Ok(new { value = 42 }));
        Assert.Equal(StatusCodes.Status200OK, capture3.StatusCode);
        using var doc3 = JsonDocument.Parse(capture3.Body);
        Assert.Equal(42, doc3.RootElement.GetProperty("data").GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task Ok_WithDelegateEnvelope_UsesDelegateDirectly_NoReflection()
    {
        // The AOT-safe Set(delegate) overload stores the factory as-is — every request
        // is a plain delegate call, zero MakeGenericType/GetMethod/Invoke.
        var factory = ResultTestHelper.CreateFactory(options =>
            options.Success.SetResponse((data, pagination, http, statusCode) =>
                new AppSuccess<object?>
                {
                    TraceId = http.TraceIdentifier,
                    StatusCode = statusCode,
                    Data = data,
                }));

        var capture = await ResultTestHelper.ExecuteAsync(factory.Ok(new { greeting = "aot" }));
        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        using var doc = JsonDocument.Parse(capture.Body);
        Assert.Equal("aot", doc.RootElement.GetProperty("data").GetProperty("greeting").GetString());
        Assert.Equal(200, doc.RootElement.GetProperty("statusCode").GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("traceId", out _));
    }

    /// <summary>A user-defined 1‑param typed success envelope for non‑paginated responses.</summary>
    private sealed class AppSuccess<T> : ISuccessEnvelope<T>
    {
        public string? TraceId { get; set; }
        public int StatusCode { get; set; }
        public T? Data { get; set; }

        public static ISuccessEnvelope<T> From(T data, IPaginationDetails? pagination, HttpContext httpContext, int statusCode) =>
            new AppSuccess<T>
            {
                TraceId = httpContext.TraceIdentifier,
                StatusCode = statusCode,
                Data = data,
            };
    }

    /// <summary>A 2‑param typed success envelope — TPage is the concrete pagination type,
    /// so STJ and Swagger serialize full DefaultOffsetInfo / DefaultCursorInfo shape.</summary>
    private sealed class AppSuccess<TData, TPage> : ISuccessEnvelope<TData>
    {
        public string? TraceId { get; set; }
        public int StatusCode { get; set; }
        public TData? Data { get; set; }
        public TPage? Pagination { get; set; }

        public static ISuccessEnvelope<TData> From(TData data, IPaginationDetails? pagination, HttpContext httpContext, int statusCode) =>
            new AppSuccess<TData, TPage>
            {
                TraceId = httpContext.TraceIdentifier,
                StatusCode = statusCode,
                Data = data,
                Pagination = pagination is TPage typed ? typed : default,
            };
    }

    /// <summary>An envelope type that forgets to implement <c>ISuccessEnvelope{T}</c> — Set must fail fast.</summary>
    private sealed class NoFromEnvelope
    {
        public object? Data { get; set; }
    }
}
