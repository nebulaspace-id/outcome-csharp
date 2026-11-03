using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Mvc;
using NebulaSpace.Outcome.AspNetCore.Pagination;
using NebulaSpace.Outcome.Samples.WebApi.Shared;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Products;

/// <summary>
/// MVC controller mirroring all Minimal API endpoints — same service, same envelopes,
/// same responses. Demonstrates the two MVC integration patterns:
/// <list type="number">
/// <item><c>[OutcomeResult]</c> filter — zero ceremony, action returns <see cref="Outcome{T}"/></item>
/// <item><c>outcome.ToActionResult(factory)</c> — explicit conversion for custom success logic</item>
/// </list>
/// </summary>
[ApiController]
[Route("mvc/products")]
[Tags("Mvc")]
[OutcomeResult]
[EnableRateLimiting("per-ip")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;
    private readonly IHttpResultFactory _http;

    public ProductsController(IProductService service, IHttpResultFactory http)
    {
        _service = service;
        _http = http;
    }

    // ── Ping ─────────────────────────────────────────────────────────────────
    // 200 with no payload, status-only envelope.

    [HttpGet("ping")]
    [ProducesResponseType<SuccessResponse<Unit>>(StatusCodes.Status200OK)]
    public IActionResult Ping() => _http.Ok().ToActionResult();

    // ── Create ───────────────────────────────────────────────────────────────
    // Async service, validation (400) + uniqueness (409) handled by the library.
    // Success → 201 with a derived Location header.

    [HttpPost]
    [ProducesResponseType<SuccessResponse<Product>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var result = await _service.CreateProduct(request);
        return result.ToActionResult(_http, product => _http.Created($"/products/{product.Id}", product));
    }

    // ── Get one ──────────────────────────────────────────────────────────────
    // 404 becomes the error envelope automatically (filter pattern).

    [HttpGet("{id:guid}")]
    [ProducesResponseType<SuccessResponse<Product>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Outcome<Product> Get(Guid id) =>
        _service.GetProduct(id);

    // ── List (offset) ────────────────────────────────────────────────────────
    // Custom offset pagination. Page/limit bound from the query string.
    // The service returns Outcome<OffsetResult<Product>>, so ToActionResult maps the
    // failure branch; the action only builds the app's metadata.

    [HttpGet]
    [ProducesResponseType<SuccessResponse<Product[]>>(StatusCodes.Status200OK)]
    public IActionResult List([FromQuery] Shared.OffsetQuery query)
    {
        var result = _service.GetProducts(query);
        return result.ToActionResult(_http, page =>
        {
            var meta = new Meta().SetPagination(page, query);
            return _http.Ok(page.Items, meta);
        });
    }

    // ── List (cursor) ───────────────────────────────────────────────────────
    // Cursor/limit bound from the query string.

    [HttpGet("cursor")]
    [ProducesResponseType<SuccessResponse<Product[]>>(StatusCodes.Status200OK)]
    public IActionResult ListCursor([FromQuery] CursorQuery query)
    {
        var result = _service.GetProductsCursor(query);
        return result.ToActionResult(_http, page =>
        {
            var meta = new Meta().SetPagination(page);
            return _http.Ok(page.Items, meta);
        });
    }

    // ── List (offset, JSON:API shape) ────────────────────────────────────────
    // Same service and envelope as List; only data differs — each item is a
    // resource object ({ type, id, attributes }). Bracketed query names are
    // handled by JsonApiOffsetQuery, exactly as on the Minimal API side.

    [HttpGet("jsonapi")]
    [ProducesResponseType<SuccessResponse<JsonApiResource<ProductAttributes>[]>>(StatusCodes.Status200OK)]
    public IActionResult ListJsonApi([FromQuery] JsonApiOffsetQuery query)
    {
        var offset = new OffsetQuery { Page = query.Page, Limit = query.Limit };
        var result = _service.GetProducts(offset);
        return result.ToActionResult(_http, page =>
        {
            var data = page.Items
                .Select(p => JsonApiResource.Create(
                    "products",
                    p.Id,
                    new ProductAttributes(p.Name, p.Price, p.Category, p.CreatedAt)))
                .ToArray();
            var meta = new Meta().SetPagination(page, offset);

            return _http.Ok(data, meta);
        });
    }

    // ── List (cursor, JSON:API shape) ───────────────────────────────────────

    [HttpGet("jsonapi/cursor")]
    [ProducesResponseType<SuccessResponse<JsonApiResource<ProductAttributes>[]>>(StatusCodes.Status200OK)]
    public IActionResult ListCursorJsonApi([FromQuery] JsonApiCursorQuery query)
    {
        var cursor = new CursorQuery { Cursor = query.Cursor, Limit = query.Limit };
        var result = _service.GetProductsCursor(cursor);
        return result.ToActionResult(_http, page =>
        {
            var data = page.Items
                .Select(p => JsonApiResource.Create(
                    "products",
                    p.Id,
                    new ProductAttributes(p.Name, p.Price, p.Category, p.CreatedAt)))
                .ToArray();
            var meta = new Meta().SetPagination(page);

            return _http.Ok(data, meta);
        });
    }

    // ── Get one (JSON:API shape) ─────────────────────────────────────────────
    // data is a single resource object, not an array.

    [HttpGet("jsonapi/{id:guid}")]
    [ProducesResponseType<SuccessResponse<JsonApiResource<ProductAttributes>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public IActionResult GetJsonApi(Guid id)
    {
        var result = _service.GetProduct(id);
        return result.ToActionResult(_http, product =>
        {
            var data = JsonApiResource.Create(
                "products",
                product.Id,
                new ProductAttributes(product.Name, product.Price, product.Category, product.CreatedAt));
            var meta = new Meta();

            return _http.Ok(data, meta);
        });
    }

    // ── Delete ───────────────────────────────────────────────────────────────
    // Success has no payload (Unit) → 204, or 404 when missing (filter pattern).

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Outcome<Unit> Delete(Guid id) =>
        _service.DeleteProduct(id);
}