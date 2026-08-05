# NebulaSpace Outcome

A lightweight result pattern (`Outcome<T>`) library for .NET with an ASP.NET Core integration that maps failures to HTTP responses — no exception-driven control flow.

- **NebulaSpace.Outcome** — the core `Outcome<T>` / `Failure` types, no dependencies.
- **NebulaSpace.Outcome.AspNetCore** — helpers to turn outcomes into `IResult` responses, envelope middleware, and customizable failure mapping.

---

## Quick start

### 1. Install

```bash
dotnet add package NebulaSpace.Outcome
dotnet add package NebulaSpace.Outcome.AspNetCore   # optional, for ASP.NET Core
```

### 2. Return an `Outcome<T>` from your service

```csharp
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Failures;

public sealed class GreetingService
{
    public Outcome<string> Greet(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? Outcome.Fail(new BadRequestFailure("Nama tidak boleh kosong."))
            : Outcome.Success($"Halo, {name}!");
}
```

### 3. Map it to an HTTP response

```csharp
using NebulaSpace.Outcome.AspNetCore;
using NebulaSpace.Outcome.AspNetCore.Http;

app.MapGroup("/greet")
   .MapGet("/{name}", (string name, IGreetingService greetingService) =>
       greetingService.Greet(name).ToHttpResult(
           greeting => HttpResult.Ok(new { greeting = greeting ?? "" })));
```

When the outcome succeeds, `ToHttpResult` invokes your `onSuccess` handler; on failure it produces the matching HTTP error response automatically.

---

## The core: `Outcome<T>`

A discriminated-union style type: either a **success** (with a `Value`) or a **failure** (with a `Failure`).

| Member | Description |
| --- | --- |
| `Outcome<T>.IsSuccess` | `true` when the operation succeeded |
| `Outcome<T>.IsFailure` | `true` when the operation failed |
| `Outcome<T>.Value` | The payload on success |
| `Outcome<T>.Failure` | The `Failure` on failure |
| `Outcome<T>.Match(onSuccess, onFailure)` | Pattern-match to a single value |
| `Outcome.Success()` | Create a `Unit`-style success |
| `Outcome.Success(value)` | Create a success with a payload |
| `Outcome.Fail(failure)` | Create a failure |

### Creating outcomes

```csharp
// Success
Outcome<string> ok = Outcome.Success("hello");
Outcome<Order> ok2 = order;          // implicit conversion from the value

// Failure
Outcome<string> fail = Outcome.Fail(new NotFoundFailure("Order not found"));
```

### Matching

`Match` lets you collapse both branches into one value — the idiomatic way to handle an outcome:

```csharp
var display = result.Match(
    onSuccess: value => $"OK: {value}",
    onFailure: failure => $"Error: {failure.Message}");
```

### Pattern-matching over outcomes

Outcomes play nicely with C# `switch` expressions:

```csharp
string msg = result switch
{
    { IsSuccess: true, Value: var v } => $"Value is {v}",
    { IsFailure: true, Failure: var f } => $"Failed with {f}",
    _ => "unreachable",
};
```

---

## Failures

Failures are immutable records derived from `Failure`:

```csharp
public abstract record Failure;
```

```csharp
public sealed record NotFoundFailure(string Message) : Failure;
```

### Built-in failures

These ship in the **AspNetCore** package and map to HTTP status codes automatically:

| Failure | HTTP status |
| --- | --- |
| `BadRequestFailure` | 400 |
| `UnauthorizedFailure` | 401 |
| `ForbiddenFailure` | 403 |
| `NotFoundFailure` | 404 |
| `ConflictFailure` | 409 |
| `ValidationFailure` | 400 (with field errors) |

### Custom failures

Subclass `Failure` for your own domain errors and map them with `MapFailure`:

```csharp
public sealed record QuotaExceededFailure(string Message) : Failure;
```

```csharp
builder.Services.AddResultPattern(options =>
{
    options.MapFailure<QuotaExceededFailure>(f =>
        Results.UnprocessableEntity(ErrorResponse.UnprocessableEntity(f.Message)));
});
```

---

## ASP.NET Core integration

### Register the services

```csharp
builder.Services.AddResultPattern(options =>
{
    options.EnvelopeSuccess = false;                    // wrap successes in SuccessResponse<T>?

    // Inject extra properties into every error envelope:
    options.AddErrorEnvelopeProperty("environment", _ => builder.Environment.EnvironmentName);

    // Map your own failure types:
    options.MapFailure<QuotaExceededFailure>(f =>
        Results.UnprocessableEntity(ErrorResponse.UnprocessableEntity(f.Message)));
});

var app = builder.Build();
app.UseResultPattern();          // error-envelope middleware (traceId injection)
```

`AddResultPattern` registers `ResultPatternOptions` in the DI container and, by default, adds a `traceId` and `path` property to every error envelope. `UseResultPattern` adds the middleware that injects these into the response body for HTTP status >= 400.

### From outcome to response

`OutcomeHttpExtensions` (namespace `NebulaSpace.Outcome.AspNetCore.Http`) bridges outcomes and `IResult`:

```csharp
// Handle both branches with one call:
result.ToHttpResult(order => HttpResult.Created(order, o => o.Id));

// Failure-only, when you already have the failure:
failure.ToErrorResponse();
```

### `HttpResult` factory helpers

`HttpResult` (namespace `NebulaSpace.Outcome.AspNetCore.Http`) builds responses while honoring `EnvelopeSuccess`:

| Helper | Behavior |
| --- | --- |
| `HttpResult.Ok(data)` | 200, wrapped or raw per `EnvelopeSuccess` |
| `HttpResult.Ok(data, pagination)` | 200 with a `Pagination` envelope |
| `HttpResult.Created(data, idSelector)` | 201 with `Location` derived from the request path |
| `HttpResult.NoContent()` | 204 |
| `HttpResult.BadRequest()` / `NotFound()` / `Conflict()` / … | 4xx with an `ErrorResponse` envelope |

### `Created` — no manual URI writing

`HttpResult.Created` builds the `Location` header for you, so you never write a URL by hand:

```csharp
// Before — you write the URI yourself:
return Results.Created($"/orders/{order.Id}", order);

// After — only the id is supplied, the path comes from the request:
return result.ToHttpResult(order => HttpResult.Created(order, o => o.Id));
// POST /orders        → Location: /orders/{id}
// POST /admin/orders  → Location: /admin/orders/{id}
```

`HttpResult.Created(data, idSelector)` reads the current path from `HttpContext.Request.Path` when the response executes — the framework passes the per-request `HttpContext` into `IResult.ExecuteAsync`. You only provide a function that extracts the new entity's id from the payload — the path is always derived from the request, so it can never drift out of sync with the route that served it.

### Pagination

Three types work together:

| Type | Role |
| --- | --- |
| `PageQuery` | Incoming pagination parameters (`page`, `pageSize`) bound from the query string |
| `PagedResult<T>` | Outgoing page from the service: `Items` + `TotalCount` |
| `Pagination` | Response metadata: `Page`, `PageSize`, `TotalPages`, `TotalItems` |

**Service** returns a `PagedResult<T>` (the `page`/`pageSize` come from the request, so the service only reports the raw `TotalCount`):

```csharp
public PagedResult<Order> GetOrders(PageQuery pageQuery)
{
    var items = _orders
        .Skip(pageQuery.Offset)          // (Page - 1) * PageSize
        .Take(pageQuery.PageSize)
        .ToArray();

    return new PagedResult<Order> { Items = items, TotalCount = _orders.Count };
}
```

**Endpoint** binds `PageQuery` with `[AsParameters]` and maps the page into a response:

```csharp
group.MapGet("/", (IOrderService orderService, [AsParameters] PageQuery pageQuery) =>
    Outcome.Success(orderService.GetOrders(pageQuery)).ToHttpResult(page =>
        HttpResult.Ok(page.Items, Pagination.Create(pageQuery, (int)page.TotalCount))));
```

- `[AsParameters]` binds each property of `PageQuery` from the query string (`?page=2&pageSize=20`).
- `PageQuery.Offset` gives you the 0-based offset directly, so you never write `(Page - 1) * PageSize` by hand.
- `PageQuery` clamps `page` to `1..25` and `pageSize` to `1..50` by default, so a client cannot ask for an unbounded page.

> **Note:** `[AsParameters]` is a **framework feature** (part of Minimal API binding), available since **.NET 7**. The library itself does not depend on it — you can bind `PageQuery` manually on .NET 6 (e.g. `[FromQuery] int page`). Only use `[AsParameters]` in your endpoint if your app targets .NET 7+.

---

## Response envelopes

When `EnvelopeSuccess` is enabled, successful responses look like:

```json
{
  "statusCode": 200,
  "data": { "greeting": "Halo, Enrico!" }
}
```

Error responses always use an envelope (regardless of `EnvelopeSuccess`):

```json
{
  "statusCode": 404,
  "traceId": "0HLMS12B6V4H6:00000001",
  "path": "/greet/Unknown",
  "message": "Not found"
}
```

---

## Error envelope middleware

`UseResultPattern()` installs a middleware that:

- Buffers the response body of status >= 400 responses.
- Injects each registered `ErrorEnvelopeProperties` entry (default: `traceId`, `path`) into the JSON body.
- Passes through everything else untouched (non-JSON, status < 400, unknown shapes).

Register additional properties with `AddErrorEnvelopeProperty`. Properties are computed **per request** from `HttpContext`.

---

## Why `Outcome<T>` instead of exceptions?

- **Explicit contracts** — the failure is part of the method signature, not a side channel.
- **No `try/catch` for control flow** — expected failures are values, not exceptions.
- **Automatic HTTP mapping** — failures become correct status codes with one call.
- **Traceable responses** — every error envelope carries a `traceId`.

---

## Packages

| Package | Contents | Targets |
| --- | --- | --- |
| `NebulaSpace.Outcome` | `Outcome`, `Outcome<T>`, `Failure` — no external dependencies | **netstandard2.0** (.NET Framework 4.6.1+, .NET Core 2.0+, all .NET 5+) |
| `NebulaSpace.Outcome.AspNetCore` | `HttpResult`, `OutcomeHttpExtensions`, failures, envelope middleware, options — depends on the core package | **net6.0** (first version with Minimal API / `IResult`) |

Because NuGet picks the compatible asset, a package targeting `netstandard2.0` / `net6.0` installs on **every later runtime** — .NET 6, 7, 8, 9, 10, etc. `[AsParameters]` binding in your endpoints requires **.NET 7+** (framework feature).

---

## License

MIT
