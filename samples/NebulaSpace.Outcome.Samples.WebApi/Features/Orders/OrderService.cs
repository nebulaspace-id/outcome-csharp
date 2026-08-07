using FluentValidation;
using NebulaSpace.Outcome.AspNetCore.Failures;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

public sealed class OrderService : IOrderService
{
    private static readonly IReadOnlyList<Order> _orders = Enumerable
        .Range(1, 137)
        .Select(i => new Order(Guid.NewGuid(), $"Product {i}", i * 10m))
        .ToArray();

    private readonly IValidator<CreateOrderRequest> _validator;

    public OrderService(IValidator<CreateOrderRequest> validator)
    {
        _validator = validator;
    }

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

    public async Task<Outcome<Order>> CreateOrder(CreateOrderRequest request)
    {
        // FluentValidation: throws ValidationException on invalid input, which the
        // exception-handling middleware maps to a 400 with the field errors.
        await _validator.ValidateAndThrowAsync(request);

        // Simulate async work (persistence, external call).
        await Task.Yield();

        if (request.Product.Equals("nope", StringComparison.OrdinalIgnoreCase))
        {
            return Outcome.Fail(new ConflictFailure($"Produk '{request.Product}' sudah tidak tersedia."));
        }

        var order = new Order(Guid.NewGuid(), request.Product, request.Amount);
        return order; // implicit conversion: Order -> Outcome<Order>
    }
}
