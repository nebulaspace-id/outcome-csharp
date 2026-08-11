using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Pagination;
using NebulaSpace.Outcome.Samples.WebApi.Failures;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders");

        group.MapPost("/", (CreateOrderRequest request, IOrderService orderService, IHttpResultFactory http) =>
            orderService.CreateOrder(request).ToHttpResultAsync(http, order => http.Created(order, o => o.Id)))
            .Produces<SuccessResponse<Order>>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/", (IOrderService orderService, [AsParameters] OffsetQuery offsetQuery, IHttpResultFactory http) =>
            Outcome.Success(orderService.GetOrders(offsetQuery)).ToHttpResult(http, page =>
                http.Ok(page.Items, page.ToOffsetInfo(offsetQuery))))
            .Produces<SuccessResponse<Order[], OffsetInfo>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/cursor", (IOrderService orderService, [AsParameters] CursorQuery cursorQuery, IHttpResultFactory http) =>
            Outcome.Success(orderService.GetOrdersCursor(cursorQuery)).ToHttpResult(http, page =>
                http.Ok(page.Items, page.ToCursorInfo(cursorQuery))))
            .Produces<SuccessResponse<Order[], CursorInfo>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        // ── Custom pagination shapes (IPaginationDetails extensibility demo) ──

        group.MapGet("/offset-custom", (IOrderService orderService, [AsParameters] OffsetQuery offsetQuery, IHttpResultFactory http) =>
            Outcome.Success(orderService.GetOrders(offsetQuery)).ToHttpResult(http, page =>
                http.Ok(page.Items, OffsetPage.Create(offsetQuery, (int)page.TotalCount))))
            .Produces<SuccessResponse<Order[], OffsetPage>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/cursor-custom", (IOrderService orderService, [AsParameters] CursorQuery cursorQuery, IHttpResultFactory http) =>
            Outcome.Success(orderService.GetOrdersCursor(cursorQuery)).ToHttpResult(http, page =>
                http.Ok(page.Items, CursorPage.Create(cursorQuery, page))))
            .Produces<SuccessResponse<Order[], CursorPage>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
