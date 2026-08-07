using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

public interface IOrderService
{
    Task<Outcome<Order>> CreateOrder(CreateOrderRequest request);
    PagedResult<Order> GetOrders(PageQuery pageQuery);
}
