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
    public IActionResult Ping() => _http.Ok().ToActionResult();

    // ── Create ───────────────────────────────────────────────────────────────
    // Async service, validation (400) + uniqueness (409) handled by the library.
    // Success → 201 with a derived Location header.

    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateProductRequest request) =>
        _service.CreateProduct(request)
            .ToActionResultAsync(_http, product => _http.Created(product, p => p.Id));

    // ── Get one ──────────────────────────────────────────────────────────────
    // 404 becomes the error envelope automatically (filter pattern).

    [HttpGet("{id:guid}")]
    public Outcome<Product> Get(Guid id) =>
        _service.GetProduct(id);

    // ── List (offset) ────────────────────────────────────────────────────────
    // Custom offset pagination. Page/limit bound from the query string.

    [HttpGet]
    public IActionResult List([FromQuery] Shared.OffsetQuery query)
    {
        var result = _service.GetProducts(query);
        return Outcome.Success(result.Items)
            .ToActionResult(_http, items => _http.Ok(items, result.ToOffsetInfo(query)));
    }

    // ── List (cursor) ───────────────────────────────────────────────────────
    // Cursor/limit bound from the query string.

    [HttpGet("cursor")]
    public IActionResult ListCursor([FromQuery] CursorQuery query)
    {
        var result = _service.GetProductsCursor(query);
        return Outcome.Success(result.Items)
            .ToActionResult(_http, items => _http.Ok(items, result.ToCursorInfo(query)));
    }

    // ── Delete ───────────────────────────────────────────────────────────────
    // Success has no payload (Unit) → 204, or 404 when missing (filter pattern).

    [HttpDelete("{id:guid}")]
    public Outcome<Unit> Delete(Guid id) =>
        _service.DeleteProduct(id);
}