using NebulaSpace.Outcome.AspNetCore.Pagination;
using NebulaSpace.Outcome.Samples.WebApi.Shared;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Products;

/// <summary>Data-access contract for the product catalog. Backed by an in-memory store.</summary>
public interface IProductRepository
{
    /// <summary>Find a product by id, or <c>null</c> when not found.</summary>
    Product? GetById(Guid id);

    /// <summary>Return a page of products (offset pagination).</summary>
    Shared.OffsetResult<Product> GetAll(Shared.OffsetQuery query);

    /// <summary>Return a page of products (cursor pagination).</summary>
    CursorResult<Product> GetAllCursor(CursorQuery query);

    /// <summary>
    /// Add a product and return it with its assigned id, or <c>null</c> when another
    /// product already uses the same name. The uniqueness check and the insert share one
    /// lock, so concurrent creates cannot both pass the check.
    /// </summary>
    Product? Add(Product product);

    /// <summary>Remove a product by id. Returns <c>true</c> when deleted, <c>false</c> when not found.</summary>
    bool Delete(Guid id);

    /// <summary>Total number of products.</summary>
    int Count { get; }
}
