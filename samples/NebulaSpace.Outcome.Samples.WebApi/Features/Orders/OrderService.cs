using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Failures;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

public sealed class OrderService : IOrderService
{
    private static readonly IReadOnlyList<Order> _orders = Enumerable
        .Range(1, 137)
        .Select(i => new Order(Guid.NewGuid(), $"Product {i}", i * 10m))
        .ToArray();

    public PagedResult<Order> GetOrders(PageQuery pageQuery)
    {
        var items = _orders
            .Skip(pageQuery.Offset)
            .Take(pageQuery.PageSize)
            .ToArray();

        return new PagedResult<Order>
        {
            Items = items,
            TotalCount = _orders.Count,
        };
    }

    public Outcome<Order> CreateOrder(CreateOrderRequest request)
    {
        if (request.Amount <= 0)
        {
            return Outcome.Fail(new ValidationFailure("Data order tidak valid.", new Dictionary<string, string>
            {
                ["Amount"] = "Jumlah harus lebih dari 0."
            }));
        }

        if (request.Product.Equals("nope", StringComparison.OrdinalIgnoreCase))
        {
            return Outcome.Fail(new ConflictFailure($"Produk '{request.Product}' sudah tidak tersedia."));
        }

        var order = new Order(Guid.NewGuid(), request.Product, request.Amount);
        return order; // implicit conversion: Order -> Outcome<Order>
    }
}
