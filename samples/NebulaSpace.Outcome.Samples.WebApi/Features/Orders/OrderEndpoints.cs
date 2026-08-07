using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Pagination;
using NebulaSpace.Outcome.Samples.WebApi.Failures;
using NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders");

        group.MapPost("/", (CreateOrderRequest request, IOrderService orderService, IHttpResultFactory http) =>
            orderService.CreateOrder(request).ToHttpResultAsync(http, order => http.Created(order, o => o.Id)))
            .Produces<DefaultSuccessResponse<Order>>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/", (IOrderService orderService, [AsParameters] PageQuery pageQuery, IHttpResultFactory http) =>
            Outcome.Success(orderService.GetOrders(pageQuery)).ToHttpResult(http, page =>
                http.Ok(page.Items, PageInfo.Create(pageQuery, (int)page.TotalCount))))
            .Produces<DefaultSuccessResponse<Order[]>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
