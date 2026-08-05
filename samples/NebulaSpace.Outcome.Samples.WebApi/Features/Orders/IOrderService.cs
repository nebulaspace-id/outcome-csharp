using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

public interface IOrderService
{
    Outcome<Order> CreateOrder(CreateOrderRequest request);
    PagedResult<Order> GetOrders(PageQuery pageQuery);
}
