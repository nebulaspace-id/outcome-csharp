namespace NebulaSpace.Outcome.Samples.WebApi.Features.Products;

/// <summary>Request body for creating a new product.</summary>
public sealed record CreateProductRequest(string Name, decimal Price, string Category);
