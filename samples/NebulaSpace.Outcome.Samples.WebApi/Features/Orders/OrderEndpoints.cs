using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders");

        group.MapPost("/", (CreateOrderRequest request, IOrderService orderService) =>
            orderService.CreateOrder(request).ToHttpResult(order => HttpResult.Created(order, o => o.Id)));

        group.MapGet("/", (IOrderService orderService, [AsParameters] PageQuery pageQuery) =>
            Outcome.Success(orderService.GetOrders(pageQuery)).ToHttpResult(page =>
                HttpResult.Ok(page.Items, Pagination.Create(pageQuery, (int)page.TotalCount))));

        return app;
    }
}
