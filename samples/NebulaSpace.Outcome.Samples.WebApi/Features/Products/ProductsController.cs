using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

    // ── Delete ───────────────────────────────────────────────────────────────
    // Success has no payload (Unit) → 204, or 404 when missing (filter pattern).

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Outcome<Unit> Delete(Guid id) =>
        _service.DeleteProduct(id);
}