namespace NebulaSpace.Outcome.Samples.WebApi.Features.Products;

/// <summary>A product in the catalog.</summary>
public sealed record Product(
    Guid Id,
    string Name,
    decimal Price,
    string Category,
    DateTime CreatedAt
);
