using FluentValidation;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Products;

/// <summary>
/// Orchestrates the product catalog. It owns validation and business rules,
/// delegates data access to <see cref="IProductRepository"/>, and returns
/// <see cref="Outcome{T}"/> so the failure is explicit and forced on the caller.
/// </summary>
public sealed class ProductService : IProductService
{
    private readonly IProductRepository _repo;
    private readonly IValidator<CreateProductRequest> _validator;

    public ProductService(IProductRepository repo, IValidator<CreateProductRequest> validator)
    {
        _repo = repo;
        _validator = validator;
    }

    public Outcome<Product> GetProduct(Guid productId)
    {
        var product = _repo.GetById(productId);
        if (product is null)
            return Outcome.Fail(new ProductNotFound(productId));

        return product; // implicit conversion: Product → Outcome<Product>
    }

    public OffsetResult<Product> GetProducts(OffsetQuery query) =>
        _repo.GetAll(query);

    public CursorResult<Product> GetProductsCursor(CursorQuery query) =>
        _repo.GetAllCursor(query);

    public async Task<Outcome<Product>> CreateProduct(CreateProductRequest request)
    {
        // FluentValidation throws ValidationException on invalid input — mapped to
        // 400 with field errors by the exception-handling middleware (Program.cs).
        await _validator.ValidateAndThrowAsync(request);

        // Business rule: product names must be unique. An expected conflict, so it
        // is an outcome, not an exception.
        if (_repo.ExistsByName(request.Name))
            return Outcome.Fail(new DuplicateProductName(request.Name));

        return _repo.Add(new Product(
            Id: Guid.NewGuid(),
            Name: request.Name,
            Price: request.Price,
            Category: request.Category,
            CreatedAt: DateTime.UtcNow
        ));
    }

    public Outcome<Unit> DeleteProduct(Guid productId)
    {
        if (!_repo.Delete(productId))
            return Outcome.Fail(new ProductNotFound(productId));

        return Outcome.Success();
    }
}
