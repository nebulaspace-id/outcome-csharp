# Outcome sample — Product Catalog

A small, production-shaped Web API that shows what the library looks like in use: one
success envelope, one error envelope, and the same endpoints written twice — Minimal API and
MVC — sharing the same service layer.

The sample is **not** published to NuGet (`IsPackable = false`). It exists as a template to
copy from.

## Run it

```bash
dotnet run --project samples/NebulaSpace.Outcome.Samples.WebApi
```

Then open <http://localhost:5232/swagger> or fire the requests in
[`NebulaSpace.Outcome.Samples.WebApi.http`](NebulaSpace.Outcome.Samples.WebApi.http).

## What to compare

| Request | Produced by | Status | Body |
| --- | --- | --- | --- |
| `GET /products/ping` | handler, `Outcome<Unit>` → `http.Ok()` | 200 | success envelope, no `data` |
| `POST /products` | handler, validation failure | 400 | error envelope, `validation_error`, per-field `errors` |
| `POST /products` (same name) | repository returns `null` → `Outcome.Fail` | 409 | `duplicate_product_name` |
| `GET /products/{id}` (missing) | handler, `IHttpFailure` self-maps | 404 | `product_not_found` |
| `GET /nothing-here` | routing, handler never ran | 404 | `not_found` via `UseOutcomeStatusCodePages()` |
| `DELETE /products` | routing, method mismatch | 405 | `method_not_allowed` via `UseOutcomeStatusCodePages()` |
| `POST /products` with `text/plain` | routing, no formatter | 415 | `unsupported_media_type` via `UseOutcomeStatusCodePages()` |
| `POST /products` with malformed JSON | `BadHttpRequestException` | 400 | `bad_request` via the exception handler |
| `POST /mvc/products` with malformed JSON | MVC model state | 400 | `validation_error` via `InvalidModelStateResponseFactory` |
| `GET /products` | free-form offset list | 200 | success envelope, `data: Product[]`, `meta.pagination` |
| `GET /products/jsonapi?page[number]=2&page[size]=5` | sample `JsonApiOffsetQuery` | 200 | JSON:API-shaped `data` + `meta.pagination` |
| `GET /products/jsonapi/cursor?page[size]=5` | sample `JsonApiCursorQuery` | 200 | JSON:API-shaped `data` + `meta.pagination` |
| `GET /products/jsonapi/{id}` | JSON:API single resource | 200 | `data: { type, id, attributes }` |
| repeated `GET /products/ping` | rate limiter | 429 | `rate_limit_exceeded`, same envelope |

The point of the last rows: application code, the MVC filter, the exception handler, routing,
and the rate limiter all converge on the same error shape. A client needs exactly one parser.

## Layout

```
Features/Products/
  ProductEndpoints.cs        Minimal API surface
  ProductsController.cs      MVC mirror, same service
  ProductService.cs          business rules → Outcome<T>
  ProductAttributes.cs       JSON:API attributes for a product (no id)
  InMemoryProductRepository.cs  storage; duplicate name returns null under one lock
  ProductErrors.cs           failures that self-map via IHttpFailure
Shared/
  SuccessResponse.cs         app-specific success envelope
  ErrorResponse.cs           app-specific error envelope
  Pagination.cs              offset/cursor meta; reuses the library's page math
  JsonApiOffsetQuery.cs      bracketed page[number] / page[size] binding (sample-only)
  JsonApiCursorQuery.cs      bracketed page[cursor] / page[size] binding (sample-only)
  JsonApiResource.cs         JSON:API resource object + static factory (sample-only)
Program.cs                   envelope registration, mapper setup, middleware order
```

## Two details worth copying

**Middleware order.** `UseOutcomeExceptionHandler()` comes first so it wraps everything.
`UseOutcomeStatusCodePages()` comes next, before authentication, authorization, and routing,
so it can observe the status codes those middleware set:

```csharp
app.UseOutcomeExceptionHandler();
app.UseOutcomeStatusCodePages();
app.UseRouting();
app.UseRateLimiter();
```

**Duplicate-name handling.** `ProductService` does not pre-check the name. The repository
checks uniqueness and inserts under one lock and returns `null` when the name is taken, so two
concurrent creates cannot both pass the check. `null` becomes
`Outcome.Fail(new DuplicateProductName(...))` — an expected 409, not an exception.

## Two data shapes, one envelope

The envelope is configured once for the whole app, so the only thing an endpoint can
choose per request is the shape of `data`. This sample shows both:

| Free-form | JSON:API-shaped |
| --- | --- |
| `GET /products?page=2&limit=10` | `GET /products/jsonapi?page[number]=2&page[size]=5` |
| `GET /products/cursor?limit=10` | `GET /products/jsonapi/cursor?page[cursor]=...&page[size]=5` |
| `GET /products/{id}` | `GET /products/jsonapi/{id}` |

The MVC controller mirrors the same three routes under `/mvc/products/jsonapi`, so both
integration styles produce the same `data` shape.

Free-form returns the domain model: `data: Product` or `data: Product[]`. The `/jsonapi`
prefix returns resource objects instead:

```json
{
  "statusCode": 200,
  "data": [
    {
      "type": "products",
      "id": "3c999947-6b69-4404-bf52-97e7555f656d",
      "attributes": {
        "name": "Monitor Stand",
        "price": 147.64,
        "category": "Electronics",
        "createdAt": "2026-09-18T07:09:28.4586591Z"
      }
    }
  ],
  "meta": { "pagination": { ... }, "timestamp": "..." }
}
```

A single resource uses the same factory, and `data` becomes an object rather than an
array. An empty page serializes as `"data": []`.

`JsonApiResource.Create("products", p.Id, new ProductAttributes(...))` builds each item.
`ProductAttributes` excludes `Id` because JSON:API keeps the identifier on the resource
object, never inside `attributes`. `meta` is always built into a local first, then passed
to `http.Ok(...)` — the factory never constructs it inline.

The envelope still carries `statusCode` and the error body is still `ErrorResponse`, so
this is a hybrid body, not a strict JSON:API document. A second app would be needed to
demo a different envelope, a different error shape, or a different content type: those are
app-wide settings, not per-endpoint.

## Bracketed pagination queries

`JsonApiOffsetQuery` and `JsonApiCursorQuery` bind the bracketed JSON:API query names:

```text
GET /products/jsonapi?page[number]=2&page[size]=5
GET /products/jsonapi/cursor?page[cursor]=...&page[size]=5
```

They are **sample types**, not part of the library. `NebulaSpace.Outcome.AspNetCore` ships
`DefaultOffsetQuery` and `DefaultCursorQuery`, which bind flat `page`/`limit` and
`cursor`/`limit` names. Copy these two classes when an API follows the bracketed convention;
the service layer does not change because the endpoint maps the bound values to the existing
`OffsetQuery` / `CursorQuery`.

Both types treat their query keys as optional: omitting `page[number]` uses page 1, omitting
`page[size]` uses 10. Values are clamped (`Page` to `1..25`, `Limit` to `1..50`) and
`Offset` is computed in 64-bit arithmetic.

## Capability map

| Capability | Where | Shape |
| --- | --- | --- |
| Success envelope, no payload | `GET /products/ping` | `{ statusCode, meta }` |
| Free-form payload | `GET /products`, `/products/cursor`, `/products/{id}` | `data: Product` / `Product[]` |
| JSON:API collection | `GET /products/jsonapi`, `/products/jsonapi/cursor` | `data: [ { type, id, attributes } ]` |
| JSON:API single resource | `GET /products/jsonapi/{id}` | `data: { type, id, attributes }` |
| Empty page | `GET /products/jsonapi?page[number]=25&page[size]=50` | `data: []` |
| Per-field validation | `POST /products` with an invalid body | `errors` map, 400 |
| Self-mapping failure | `POST /products` with a duplicate name | 409 |
| 204 without a body | `DELETE /products/{id}` | empty |
| Framework status pages | unknown route, wrong method, rate limit | 404 / 405 / 429 envelope |
| MVC mirror | `/mvc/products` | same as Minimal API |
| MVC mirror, JSON:API | `/mvc/products/jsonapi` | same as Minimal API `/jsonapi` |

Two error paths are configured in `Program.cs` but have no HTTP trigger: the
`QuotaExceededFailure` mapping (422) and the `DomainException` mapping (422). The unhandled
exception path (generic 500 envelope) is likewise only reachable from a real bug.

## Rate limit

`per-ip` allows 10 requests per minute and partitions by client IP. If a request unexpectedly
returns 429 while you are exploring, that is the limiter, not a bug.
