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
| repeated `GET /products/ping` | rate limiter | 429 | `rate_limit_exceeded`, same envelope |

The point of the last rows: application code, the MVC filter, the exception handler, routing,
and the rate limiter all converge on the same error shape. A client needs exactly one parser.

## Layout

```
Features/Products/
  ProductEndpoints.cs        Minimal API surface
  ProductsController.cs      MVC mirror, same service
  ProductService.cs          business rules → Outcome<T>
  InMemoryProductRepository.cs  storage; duplicate name returns null under one lock
  ProductErrors.cs           failures that self-map via IHttpFailure
Shared/
  SuccessResponse.cs         app-specific success envelope
  ErrorResponse.cs           app-specific error envelope
  Pagination.cs              offset/cursor meta; reuses the library's page math
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

## Rate limit

`per-ip` allows 10 requests per minute and partitions by client IP. If a request unexpectedly
returns 429 while you are exploring, that is the limiter, not a bug.
