using System.Collections.Concurrent;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.Samples.WebApi.Shared;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Products;

/// <summary>
/// In-memory product store seeded with 25 products. The dictionary gives O(1)
/// lookup by id; the ordered list preserves insertion order for pagination.
///
/// <para>
/// The sample is registered as a singleton, so every mutation and every ordered
/// read takes the same lock. Without it, <c>List&lt;T&gt;</c> can throw or return
/// torn pages while another request adds or removes a product.
/// </para>
/// </summary>
public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<Guid, Product> _products = new();
    private readonly List<Product> _ordered = [];
    private readonly object _gate = new();

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
            var product = new Product(
                Id: Guid.NewGuid(),
                Name: name,
                Price: Math.Round((decimal)(random.NextDouble() * 190 + 10), 2),
                Category: categories[i % categories.Length],
                CreatedAt: DateTime.UtcNow.AddDays(-random.Next(1, 90)));

            _products[product.Id] = product;
            _ordered.Add(product);
        }
    }

    public int Count => _products.Count;

    public Product? GetById(Guid id) =>
        _products.TryGetValue(id, out var product) ? product : null;

    public Shared.OffsetResult<Product> GetAll(Shared.OffsetQuery query)
    {
        lock (_gate)
        {
            return new Shared.OffsetResult<Product>
            {
                // Offset is capped by the query type, but clamp again so a custom
                // query can never skip past int.MaxValue or request a negative page.
                Items = _ordered
                    .Skip(Math.Max(0, query.Offset))
                    .Take(Math.Max(0, query.Limit))
                    .ToArray(),
                TotalCount = _ordered.Count,
            };
        }
    }

    public CursorResult<Product> GetAllCursor(CursorQuery query)
    {
        lock (_gate)
        {
            var startIndex = Math.Clamp(DecodeCursor(query.Cursor), 0, _ordered.Count);
            var limit = Math.Max(0, query.Limit);

            var items = _ordered
                .Skip(startIndex)
                .Take(limit)
                .ToArray();

            var endIndex = startIndex + items.Length;
            var hasMore = endIndex < _ordered.Count;

            return new CursorResult<Product>
            {
                Items = items,
                PreviousCursor = startIndex == 0 ? null : Math.Max(0, startIndex - limit).ToString(),
                NextCursor = hasMore ? endIndex.ToString() : null,
                TotalCount = _ordered.Count,
            };
        }
    }

    public Product? Add(Product product)
    {
        // The uniqueness check and the insert happen under one lock so two concurrent
        // creates cannot both pass the check (TOCTOU).
        lock (_gate)
        {
            // Defense-in-depth invariant: the service generates fresh ids, so reaching
            // this guard means a real bug.
            if (_products.ContainsKey(product.Id))
                throw new DomainException($"Invariant violated: product id '{product.Id}' already exists.");

            // A duplicate name is an expected failure, not a bug: return null and let
            // the service turn it into DuplicateProductName (409).
            if (_ordered.Any(p => p.Name.Equals(product.Name, StringComparison.OrdinalIgnoreCase)))
                return null;

            _products[product.Id] = product;
            _ordered.Add(product);
            return product;
        }
    }

    public bool Delete(Guid id)
    {
        lock (_gate)
        {
            if (!_products.TryRemove(id, out var product))
                return false;

            _ordered.Remove(product);
            return true;
        }
    }

    /// <summary>
    /// A cursor is just the 0-based index into the ordered list. A production
    /// implementation would base64-encode an opaque token; this sample keeps it
    /// readable to demonstrate the mechanics.
    /// </summary>
    private static int DecodeCursor(string? cursor) =>
        string.IsNullOrEmpty(cursor) || !int.TryParse(cursor, out var index) ? 0 : index;
}
