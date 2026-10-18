using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Pagination;
using NebulaSpace.Outcome.Samples.WebApi.Shared;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Products;

/// <summary>
/// Product business logic. Methods that can fail return <see cref="Outcome{T}"/>
/// so the failure is part of the contract; the endpoint decides how to render it.
/// </summary>
public interface IProductService
{
    /// <summary>Get a single product by id, or <see cref="ProductNotFound"/>.</summary>
    Outcome<Product> GetProduct(Guid productId);

    /// <summary>
    /// Get a page of products (offset pagination), or a failure — a paged read is a
    /// service call like any other, so it returns <see cref="Outcome{T}"/> and the
    /// endpoint's <c>ToResult</c> maps the failure branch for free.
    /// </summary>
    Outcome<Shared.OffsetResult<Product>> GetProducts(Shared.OffsetQuery query);

    /// <summary>Get a page of products (cursor pagination), or a failure.</summary>
    Outcome<CursorResult<Product>> GetProductsCursor(CursorQuery query);

    /// <summary>Create a product after validation and a uniqueness check.</summary>
    Task<Outcome<Product>> CreateProduct(CreateProductRequest request);

    /// <summary>Delete a product by id, or <see cref="ProductNotFound"/>.</summary>
    Outcome<Unit> DeleteProduct(Guid productId);
}
