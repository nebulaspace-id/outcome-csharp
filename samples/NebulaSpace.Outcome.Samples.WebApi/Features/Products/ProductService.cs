using FluentValidation;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Failures;
using NebulaSpace.Outcome.Samples.WebApi.Shared;

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

    // Paged reads return Outcome<T> too: an implicit conversion lifts the repository's
    // OffsetResult into a success, so the method stays ready to fail (a guard, a
    // permission check, a downstream call) without changing the endpoint's shape.
    public Outcome<Shared.OffsetResult<Product>> GetProducts(Shared.OffsetQuery query) =>
        _repo.GetAll(query);

    public Outcome<CursorResult<Product>> GetProductsCursor(CursorQuery query) =>
        _repo.GetAllCursor(query);

    public async Task<Outcome<Product>> CreateProduct(CreateProductRequest request)
    {
        // Validation is an expected outcome, not an exception: return a 400
        // ValidationFailure so the endpoint maps it like any other failure.
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return Outcome.Fail(new ValidationFailure("Validation Error", errors));
        }

        // Business rule: product names must be unique. An expected conflict, so it
        // is an outcome, not an exception. The repository re-checks under a lock.
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
