namespace NebulaSpace.Outcome.Samples.WebApi.Features.Products;

/// <summary>
/// JSON:API attributes for a product. Deliberately excludes <c>Id</c>: the identifier
/// lives on the resource object, and JSON:API forbids <c>id</c> inside
/// <c>attributes</c>.
/// </summary>
public sealed record ProductAttributes(
    string Name,
    decimal Price,
    string Category,
    DateTime CreatedAt);
