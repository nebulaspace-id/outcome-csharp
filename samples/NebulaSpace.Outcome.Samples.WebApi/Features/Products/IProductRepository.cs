using NebulaSpace.Outcome.AspNetCore.Pagination;
using NebulaSpace.Outcome.Samples.WebApi.Shared;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Products;

/// <summary>Data-access contract for the product catalog. Backed by an in-memory store.</summary>
public interface IProductRepository
{
    /// <summary>Find a product by id, or <c>null</c> when not found.</summary>
    Product? GetById(Guid id);

    /// <summary>Check whether a product with the given name already exists.</summary>
    bool ExistsByName(string name);

    /// <summary>Return a page of products (offset pagination).</summary>
    Shared.OffsetResult<Product> GetAll(Shared.OffsetQuery query);

    /// <summary>Return a page of products (cursor pagination).</summary>
    CursorResult<Product> GetAllCursor(CursorQuery query);

    /// <summary>Add a product and return it with its assigned id.</summary>
    Product Add(Product product);

    /// <summary>Remove a product by id. Returns <c>true</c> when deleted, <c>false</c> when not found.</summary>
    bool Delete(Guid id);

    /// <summary>Total number of products.</summary>
    int Count { get; }
}
