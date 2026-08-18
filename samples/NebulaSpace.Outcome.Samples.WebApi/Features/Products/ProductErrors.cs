using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Products;

// ──────────────────────────────────────────────────────────────────────────
// Application-defined failures. These are expected outcomes (values), not
// exceptions — the service returns them and the endpoint turns them into an
// HTTP error. They implement IHttpFailure so the failure carries its own HTTP
// intent (Open/Closed): add a new failure type and it maps itself, no
// registration needed in Program.cs.
// ──────────────────────────────────────────────────────────────────────────

/// <summary>
/// The requested product does not exist. Maps itself to 404 Not Found.
/// </summary>
public sealed class ProductNotFound : Failure, IHttpFailure
{
    public ProductNotFound(Guid productId)
        : base($"Product '{productId}' not found.", "product_not_found")
    {
        ProductId = productId;
    }

    /// <summary>The id that was not found.</summary>
    public Guid ProductId { get; }

    public FailureDetails ToDetails() => new(StatusCodes.Status404NotFound, Message, Code);
}

/// <summary>
/// A product with the given name already exists. Maps itself to 409 Conflict.
/// </summary>
public sealed class DuplicateProductName : Failure, IHttpFailure
{
    public DuplicateProductName(string name)
        : base($"A product with name '{name}' already exists.", "duplicate_product_name")
    {
        Name = name;
    }

    /// <summary>The duplicate name.</summary>
    public string Name { get; }

    public FailureDetails ToDetails() => new(StatusCodes.Status409Conflict, Message, Code);
}
