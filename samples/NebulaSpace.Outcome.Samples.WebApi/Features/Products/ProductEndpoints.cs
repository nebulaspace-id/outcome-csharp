using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Pagination;
using NebulaSpace.Outcome.Samples.WebApi.Shared;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Products;

/// <summary>
/// Product endpoints. Each handler is one line: call the service, map the outcome
/// to an HTTP response. No try/catch — expected failures and unhandled exceptions
/// both reduce to the configured error envelope.
/// </summary>
public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products");

        // Create — async service, validation (400) + uniqueness (409) handled by the
        // library. Success → 201 with a derived Location header.
        group.MapPost("/", (CreateProductRequest request, IProductService service, IHttpResultFactory http) =>
            service.CreateProduct(request).ToHttpResultAsync(http, product => http.Created(product, p => p.Id)))
            .Produces<SuccessResponse<Product>>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        // Get one — 404 becomes the error envelope automatically.
        group.MapGet("/{id:guid}", (Guid id, IProductService service, IHttpResultFactory http) =>
            service.GetProduct(id).ToHttpResult(http, product => http.Ok(product)))
            .Produces<SuccessResponse<Product>>()
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);
        
        // List (offset) — page/limit bound from the query string.
        group.MapGet("/", (IProductService service, [AsParameters] OffsetQuery query, IHttpResultFactory http) =>
            Outcome.Success(service.GetProducts(query)).ToHttpResult(http, page => http.Ok(page.Items, page.ToOffsetInfo(query))))
            .Produces<SuccessResponse<Product[], OffsetInfo>>();

        // List (keyset) — custom IPaginationDetails. Uses the product id as the
        // opaque key; AfterKey = last id on the previous page (omit for first page).
        group.MapGet("/keyset", (IProductService service, [AsParameters] KeysetQuery query, IHttpResultFactory http) =>
            Outcome.Success(service.GetProductsKeyset(query)).ToHttpResult(http, page => http.Ok(page.Items, page.ToKeysetInfo())))
            .Produces<SuccessResponse<Product[], KeysetInfo>>();

        // List (cursor) — cursor/limit bound from the query string.
        group.MapGet("/cursor", (IProductService service, [AsParameters] CursorQuery query, IHttpResultFactory http) =>
            Outcome.Success(service.GetProductsCursor(query)).ToHttpResult(http, page => http.Ok(page.Items, page.ToCursorInfo(query))))
            .Produces<SuccessResponse<Product[], CursorInfo>>();

        // Delete — success has no payload (Unit); 204, or 404 when missing.
        group.MapDelete("/{id:guid}", (Guid id, IProductService service, IHttpResultFactory http) =>
            service.DeleteProduct(id).ToHttpResult(http, _ => http.NoContent()))
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return group;
    }
}
