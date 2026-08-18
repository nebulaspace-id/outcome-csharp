using System.Collections.Concurrent;
using NebulaSpace.Outcome.AspNetCore.Pagination;
using NebulaSpace.Outcome.Samples.WebApi.Shared;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Products;

/// <summary>
/// In-memory product store seeded with 25 products. The dictionary gives O(1)
/// lookup by id; the ordered list preserves insertion order for pagination.
/// </summary>
public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<Guid, Product> _products = new();
    private readonly List<Product> _ordered;

    public InMemoryProductRepository()
    {
        // Seed 25 products across 3 categories.
        var categories = new[] { "Electronics", "Books", "Clothing" };
        var names = new[]
        {
            "Wireless Mouse", "Mechanical Keyboard", "USB-C Hub", "Monitor Stand",
            "Webcam Pro", "Laptop Sleeve", "Desk Lamp", "External SSD",
            "Noise-Canceling Headphones", "Graphics Tablet",
            "Clean Code", "Design Patterns", "Refactoring", "Domain-Driven Design",
            "The Pragmatic Programmer", "Building Microservices", "Accelerate",
            "Working Effectively with Legacy Code",
            "Cotton T-Shirt", "Denim Jacket", "Running Shoes", "Wool Sweater",
            "Leather Belt", "Baseball Cap", "Hiking Backpack"
        };

        var random = new Random(42); // deterministic seed
        foreach (var (name, i) in names.Select((name, i) => (name, i)))
        {
            AddCore(new Product(
                Id: Guid.NewGuid(),
                Name: name,
                Price: Math.Round((decimal)(random.NextDouble() * 190 + 10), 2),
                Category: categories[i % categories.Length],
                CreatedAt: DateTime.UtcNow.AddDays(-random.Next(1, 90))
            ));
        }

        _ordered = _products.Values.ToList();
    }

    public int Count => _products.Count;

    public Product? GetById(Guid id) =>
        _products.TryGetValue(id, out var product) ? product : null;

    public bool ExistsByName(string name) =>
        _products.Values.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public OffsetResult<Product> GetAll(OffsetQuery query) => new()
    {
        Items = _ordered
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToArray(),
        TotalCount = _ordered.Count,
    };

    public CursorResult<Product> GetAllCursor(CursorQuery query)
    {
        var startIndex = DecodeCursor(query.Cursor);

        var items = _ordered
            .Skip(startIndex)
            .Take(query.Limit)
            .ToArray();

        var endIndex = startIndex + items.Length;
        var hasMore = endIndex < _ordered.Count;

        return new CursorResult<Product>
        {
            Items = items,
            PreviousCursor = startIndex == 0 ? null : Math.Max(0, startIndex - query.Limit).ToString(),
            NextCursor = hasMore ? endIndex.ToString() : null,
            TotalCount = _ordered.Count,
        };
    }

    public KeysetResult<Product> GetAllKeyset(KeysetQuery query)
    {
        // Find the starting index: after the product whose Id matches AfterKey.
        var startIndex = 0;
        if (!string.IsNullOrEmpty(query.AfterKey) && Guid.TryParse(query.AfterKey, out var afterId))
        {
            var anchorIndex = _ordered.FindIndex(p => p.Id == afterId);
            if (anchorIndex >= 0)
                startIndex = anchorIndex + 1; // page starts AFTER the anchor
        }

        // Take Limit + 1 to determine HasMore without a separate count query.
        var items = _ordered
            .Skip(startIndex)
            .Take(query.Limit + 1)
            .ToArray();

        var hasMore = items.Length > query.Limit;
        var page = hasMore ? items[..query.Limit] : items;

        return new KeysetResult<Product>
        {
            Items = page,
            NextKey = hasMore ? page[^1].Id.ToString() : null,
            PreviousKey = startIndex > 0 ? _ordered[startIndex - 1].Id.ToString() : null,
            HasMore = hasMore,
            TotalItems = _ordered.Count,
        };
    }

    public Product Add(Product product)
    {
        // Defense-in-depth invariant: the service already checks uniqueness and
        // generates fresh ids, so reaching these guards means a real bug (e.g. a
        // race). An exceptional condition → throw; the exception-handling
        // middleware maps DomainException to a clean error envelope.
        if (_products.ContainsKey(product.Id))
            throw new DomainException($"Invariant violated: product id '{product.Id}' already exists.");

        if (ExistsByName(product.Name))
            throw new DomainException($"Invariant violated: product name '{product.Name}' already exists.");

        AddCore(product);
        _ordered.Add(product);
        return product;
    }

    public bool Delete(Guid id)
    {
        if (!_products.TryRemove(id, out var product))
            return false;

        _ordered.Remove(product);
        return true;
    }

    private void AddCore(Product product) => _products[product.Id] = product;

    /// <summary>
    /// A cursor is just the 0-based index into the ordered list. A production
    /// implementation would base64-encode an opaque token; this sample keeps it
    /// readable to demonstrate the mechanics.
    /// </summary>
    private static int DecodeCursor(string? cursor) =>
        string.IsNullOrEmpty(cursor) || !int.TryParse(cursor, out var index) ? 0 : index;
}
