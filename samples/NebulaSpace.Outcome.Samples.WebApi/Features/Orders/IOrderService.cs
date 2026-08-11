using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

public interface IOrderService
{
    Task<Outcome<Order>> CreateOrder(CreateOrderRequest request);
    OffsetResult<Order> GetOrders(OffsetQuery offsetQuery);
    CursorResult<Order> GetOrdersCursor(CursorQuery cursorQuery);
}
