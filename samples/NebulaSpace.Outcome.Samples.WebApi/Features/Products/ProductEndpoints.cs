using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
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
        var group = app.MapGroup("/products")
            .WithTags("Minimal Api");

        // Ping — 200 with no payload. Same envelope as every other response;
        // Data is null so WhenWritingNull omits it.
        group.MapGet("/ping", (IHttpResultFactory http) => http.Ok())
            .Produces<SuccessResponse<Unit>>();

        // Create — async service, validation (400) + uniqueness (409) handled by the
        // library. Success → 201 with a derived Location header.
        group.MapPost("/", (CreateProductRequest request, IProductService service, IHttpResultFactory http) =>
        {
            var result = service.CreateProduct(request);
            return result.ToResultAsync(http, product => http.Created($"/products/{product.Id}", product));
        })
            .Produces<SuccessResponse<Product>>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        // Get one — 404 becomes the error envelope automatically. No Meta call:
        // the envelope supplies the per-request metadata (timestamp) itself.
        group.MapGet("/{id:guid}", (Guid id, IProductService service, IHttpResultFactory http) =>
        {
            var result = service.GetProduct(id);
            return result.ToResult(http, product => http.Ok(product));
        })
            .Produces<SuccessResponse<Product>>()
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // List (offset) — custom offset pagination. Page/limit bound from the query string.
        // The service returns Outcome<OffsetResult<Product>>, so ToResult maps the
        // failure branch for free; the handler only builds the app's meta.
        group.MapGet("/", (IProductService service, [AsParameters] Shared.OffsetQuery query, IHttpResultFactory http) =>
        {
            var result = service.GetProducts(query);
            return result.ToResult(http, page =>
            {
                var meta = new Meta().SetPagination(page, query);
                return http.Ok(page.Items, meta);
            });
        })
            .Produces<SuccessResponse<Product[]>>();

        // List (cursor) — custom cursor pagination. Cursor/limit bound from the query string.
        group.MapGet("/cursor", (IProductService service, [AsParameters] CursorQuery query, IHttpResultFactory http) =>
        {
            var result = service.GetProductsCursor(query);
            return result.ToResult(http, page =>
            {
                var meta = new Meta().SetPagination(page);
                return http.Ok(page.Items, meta);
            });
        })
            .Produces<SuccessResponse<Product[]>>();

        // Delete — success has no payload (Unit); 204, or 404 when missing.
        group.MapDelete("/{id:guid}", (Guid id, IProductService service, IHttpResultFactory http) =>
        {
            var result = service.DeleteProduct(id);
            return result.ToResult(http, _ => http.NoContent());
        })
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return group;
    }
}
