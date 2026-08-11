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

    public OffsetResult<Order> GetOrders(OffsetQuery offsetQuery)
    {
        var items = _orders
            .Skip(offsetQuery.Offset)
            .Take(offsetQuery.Limit)
            .ToArray();

        return new OffsetResult<Order>
        {
            Items = items,
            TotalCount = _orders.Count,
        };
    }

    public CursorResult<Order> GetOrdersCursor(CursorQuery cursorQuery)
    {
        // Decode the cursor to find the starting index (0 for first page).
        var startIndex = 0;
        if (!string.IsNullOrEmpty(cursorQuery.Cursor))
        {
            // A real implementation would decode an opaque token (e.g. base64 JSON).
            // This sample simply parses an integer for demonstration.
            if (int.TryParse(cursorQuery.Cursor, out var decoded))
                startIndex = decoded;
        }

        var items = _orders
            .Skip(startIndex)
            .Take(cursorQuery.Limit)
            .ToArray();

        var endIndex = startIndex + items.Length;
        var hasMore = endIndex < _orders.Count;

        return new CursorResult<Order>
        {
            Items = items,
            PreviousCursor = startIndex == 0 ? null : Math.Max(0, startIndex - cursorQuery.Limit).ToString(),
            NextCursor = hasMore ? endIndex.ToString() : null,
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
