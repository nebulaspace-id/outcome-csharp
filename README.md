<div align="center">

# NebulaSpace Outcome

**A tiny, dependency-free result pattern for .NET — with a first-class ASP.NET Core integration that turns outcomes into clean HTTP responses.**

[![NuGet][outcome-shield]][outcome-nuget]
[![NuGet][aspnetcore-shield]][aspnetcore-nuget]
[![MIT License][license-shield]][license]

`Outcome<T>` · `Failure` · `IHttpResultFactory` · `SuccessEnvelope` · `ErrorEnvelope` · exception mapping

</div>

---

## Why Outcome?

Exceptions are for exceptional things. But most business errors — "not found", "duplicate",
"out of quota" — are **expected outcomes**, not bugs. When you model them as exceptions you
pay the cost of `try/catch` control flow, lost compiler help, and a muddled contract.

`Outcome<T>` makes the failure part of the method signature. A service says exactly what it
can produce — a value, or a typed failure — and the caller is **forced** to handle both
branches. No hidden throw, no `.Result`, no guesswork.

```csharp
// Before — failure is invisible, caught nowhere in particular:
public async Task<Order> CreateOrder(CreateOrderRequest request)
{
    if (await _repo.Exists(request.Product))
        throw new ConflictException("Product already exists");
    return await _repo.Save(new Order(request.Product));
}

// After — the contract is explicit, and the caller must handle both branches:
public async Task<Outcome<Order>> CreateOrder(CreateOrderRequest request)
{
    if (await _repo.Exists(request.Product))
        return Outcome.Fail(new ConflictFailure("Product already exists"));
    return await _repo.Save(new Order(request.Product));   // implicit success
}
```

The ASP.NET Core integration closes the loop: `Outcome<T>` → `IResult` in one call, with a
consistent, configurable error envelope across expected failures **and** unhandled exceptions.

---

## Quick start

**1. Install**

```bash
dotnet add package NebulaSpace.Outcome
dotnet add package NebulaSpace.Outcome.AspNetCore   # ASP.NET Core integration
```

**2. Return an `Outcome<T>` from your service**

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

**3. Map it to an HTTP response**

```csharp
using NebulaSpace.Outcome.AspNetCore.Http;

app.MapGroup("/greet")
   .MapGet("/{name}", (string name, IGreetingService service, IHttpResultFactory http) =>
       service.Greet(name).ToHttpResult(http, greeting => http.Ok(new { greeting })));
```

Success invokes your handler; failure becomes the correct HTTP error automatically —
**no `try/catch` in the endpoint, ever.**

> The full runnable sample lives in [`samples/NebulaSpace.Outcome.Samples.WebApi`](samples/NebulaSpace.Outcome.Samples.WebApi).

---

## Packages

| Package | What's inside | Targets | Dependencies |
| --- | --- | --- | --- |
| `NebulaSpace.Outcome` | `Outcome`, `Outcome<T>`, `Failure`, `Unit` | **netstandard2.0** | none |
| `NebulaSpace.Outcome.AspNetCore` | `IHttpResultFactory`, options, failure types, pagination, exception handling | **net6.0** | core + `Microsoft.AspNetCore.App` (framework reference) |

Because NuGet picks the compatible asset, a `netstandard2.0` / `net6.0` package installs on
**every later runtime** — .NET 6, 7, 8, 9, 10, and .NET Framework 4.6.1+. `[AsParameters]`
binding in your endpoints is a framework feature available since **.NET 7**.

---

## The core: `Outcome<T>`

A discriminated-union style value: either a **success** (with a `Value`) or a **failure**
(with a `Failure`). `Outcome<T>` is an **immutable `readonly struct`** — its state is
consistent by construction and can never change.

### Creating outcomes

```csharp
// Success with a value
Outcome<string> ok = Outcome.Success("hello");

// Success with no payload (Unit)
Outcome<Unit> done = Outcome.Success();

// Implicit conversion from a raw value
Outcome<Order> order = new Order(...);

// Failure
Outcome<string> failed = Outcome.Fail(new NotFoundFailure("Order not found"));
```

### Reading the state

| Member | Description |
| --- | --- |
| `IsSuccess` / `IsFailure` | Which branch you're in |
| `Value` | The payload on success (throws `InvalidOperationException` on failure) |
| `Failure` | The `Failure` on failure (throws on success) |
| `Match(onSuccess, onFailure)` | Collapse both branches into one value |
| `Deconstruct(...)` | C# `switch` / deconstruction support |

### Matching — the idiomatic handler

`Match` forces you to handle both branches and collapses them into one result:

```csharp
var display = result.Match(
    onSuccess: value   => $"OK: {value}",
    onFailure: failure => $"Error: {failure.Message}");
```

### Pattern matching over outcomes

`Deconstruct` plays nicely with C# `switch` expressions:

```csharp
string message = result switch
{
    (true, var value, _)         => $"Value is {value}",
    (false, _, var failure)      => $"Failed with {failure.Message}",
    _                            => "Unreachable",
};
```

---

## Failures

Failures are immutable values derived from `Failure`. The base type carries a human-readable
`Message` and an optional machine-readable `Code` — provide a code only when one is meaningful:

```csharp
public abstract class Failure
{
    public string Message { get; }
    public string? Code { get; }             // optional — omit when there's no meaningful code

    protected Failure(string message);                    // code = null
    protected Failure(string message, string? code);
}
```

```csharp
public sealed class NotFoundFailure : Failure
{
    public NotFoundFailure(string message) : base(message, "not_found") { }
}
```

### Built-in failures

These ship in the **AspNetCore** package and know their own HTTP intent — no registration needed:

| Failure | HTTP status | Code |
| --- | --- | --- |
| `BadRequestFailure` | 400 | `bad_request` |
| `UnauthorizedFailure` | 401 | `unauthorized` |
| `ForbiddenFailure` | 403 | `forbidden` |
| `NotFoundFailure` | 404 | `not_found` |
| `ConflictFailure` | 409 | `conflict` |
| `ValidationFailure` | 400 | `validation` |
| `InternalServerErrorFailure` | 500 | `internal_server_error` |

### Custom failures

Three ways to give a failure an HTTP response — in increasing order of power:

**1. `options.Failures.Map<TFailure>`** — register a mapping at startup. The mapper returns
HTTP intent, which the configured error envelope renders:

```csharp
builder.Services.AddOutcomeHttp(options =>
{
    options.Failures.Map<QuotaExceededFailure>(f =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));
});
```

**2. Implement `IHttpFailure`** — declare the failure's HTTP intent on the type itself.
The factory picks it up automatically (Open/Closed: new failures add HTTP behavior without
touching any mapping code):

```csharp
public sealed class QuotaExceededFailure : Failure, IHttpFailure
{
    public QuotaExceededFailure(string message) : base(message, "quota_exceeded") { }

    public FailureDetails ToDetails() =>
        new(StatusCodes.Status422UnprocessableEntity, Message, Code);
}
```

**3. A typed error envelope** — change the shape of *every* error response to your own
type, documented exactly in OpenAPI. See [Pluggable error shape](#pluggable-error-shape).

### `FromFailure` resolution order

1. **`options.Failures.Map<TFailure>`** — an explicit registration wins, even over built-in failures.
2. **`IHttpFailure`** — the failure maps itself.
3. **Unknown failures** — degrade to **500 Internal Server Error** (never throws).

A subclass of a registered failure type inherits its nearest ancestor's mapping.

---

## ASP.NET Core integration

### Register the services

```csharp
builder.Services.AddOutcomeHttp(options =>
{
    options.SuccessEnvelope.Enabled = true;          // wrap successes in DefaultSuccessResponse<T>? (default: true)

    // The error envelope is ANY type you define — serialized as-is, so OpenAPI documents
    // the exact body via .Produces<TError>(). Per-request fields are real properties.
    // ErrorResponse is defined below — see "A typed error envelope".
    options.ErrorEnvelope.Set(ErrorResponse.From);

    // Map application-defined failures to HTTP intent:
    options.Failures.Map<QuotaExceededFailure>(f =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));
});
```

`AddOutcomeHttp` registers `ResultPatternOptions` and `IHttpResultFactory` in the DI
container, so **multiple apps in one process can configure the library independently**.
The error shape is configured here — no `services.Replace`, no strategy interface to swap.

### From outcome to response

`OutcomeHttpExtensions` (namespace `NebulaSpace.Outcome.AspNetCore.Http`) bridges outcomes
and `IResult`. Inject `IHttpResultFactory` into your endpoint or service and pass it along:

```csharp
// Handle both branches with one call:
result.ToHttpResult(http, order => http.Created(order, o => o.Id));

// Async services (Task<Outcome<T>>) — await the outcome, then map:
await outcomeTask.ToHttpResultAsync(http, order => http.Created(order, o => o.Id));

// ...or with an async success handler:
await outcomeTask.ToHttpResultAsync(http, async order => await store.Save(order, http));

// Failure-only, when you already have the failure:
failure.ToErrorResponse(http);
```

### `IHttpResultFactory` helpers

| Helper | Behavior |
| --- | --- |
| `http.Ok(data)` | 200, raw or wrapped per `SuccessEnvelope.Enabled` |
| `http.Ok(data, pagination)` | 200 with pagination metadata |
| `http.Created(data, idSelector)` | 201 with `Location` derived from the request path |
| `http.NoContent()` | 204 |
| `http.BadRequest(...)` / `http.NotFound(...)` / `http.Conflict(...)` / … | 4xx with the configured error envelope |
| `http.FromFailure(failure)` | Map any failure to its response |
| `http.FromFailureDetails(details)` | Build a response from HTTP intent (used by exception mappers) |

Because it is resolved through DI, the factory honors the options registered for *that*
application — no shared static state.

### `Created` — no manual URI writing

`http.Created` builds the `Location` header for you — you only supply the new entity's id:

```csharp
// Before — you write the URI yourself:
return Results.Created($"/orders/{order.Id}", order);

// After — the path comes from the request; only the id is supplied:
return result.ToHttpResult(http, order => http.Created(order, o => o.Id));
// POST /orders        → Location: /orders/{id}
// POST /admin/orders  → Location: /admin/orders/{id}
```

The path is read from `HttpContext.Request.Path` when the response executes, so it can
never drift out of sync with the route that served it.

---

## Response envelopes

### Success

When `SuccessEnvelope.Enabled` (default), successes look like:

```json
{
  "statusCode": 200,
  "data": { "greeting": "Halo, Enrico!" }
}
```

Disable it to return the raw payload: `options.SuccessEnvelope.Enabled = false`.

### Error — clean RFC 7807 by default

Error responses **always** use an envelope (regardless of `SuccessEnvelope.Enabled`). If you
don't call `ErrorEnvelope.Set(...)`, the default is minimal RFC 7807
`DefaultProblemDetails` — only `title`, `status`, and `detail`:

```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Order not found"
}
```

No `type`, `traceId`, `path`, `errorCode`, or custom properties unless you configure them.
`DefaultProblemDetails` is non-nullable, so its OpenAPI schema is clean and `title`,
`status`, and `detail` are always present — `detail` is an empty string when the failure has
no message. The `title` comes from the status's standard reason phrase. Call
`ErrorEnvelope.Set(...)` only when you want a different error body.

### A typed error envelope (recommended)

`ErrorEnvelopeOptions.Set<TEnvelope>` makes the error body **any class you define**. The
library serializes that type as-is and writes the HTTP status from the failure's intent
(`FailureDetails.StatusCode`), so:

- **OpenAPI documents the exact body** — `.Produces<ErrorResponse>(404)` in your endpoint
  describes precisely what the library writes.
- **Per-request fields are real C# properties** — `traceId`, `path`, `environment` are
  typed, not string keys in a runtime dictionary. Typos are caught at compile time.
- **One body everywhere** — expected failures, `Failures.Map` results, and mapped
  exceptions all reduce to a `FailureDetails`, which `Set` turns into this one shape.

```csharp
// Your error shape — inherits nothing, serializes fully:
public sealed class ErrorResponse
{
    public int? StatusCode { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }
    public string? TraceId { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static ErrorResponse From(FailureDetails details, HttpContext context) => new()
    {
        StatusCode = details.StatusCode,
        Code = details.Code,
        Message = details.Message,
        TraceId = context.TraceIdentifier,
        Errors = details.Errors,   // RFC 9457: field name → array of messages
    };
}

builder.Services.AddOutcomeHttp(options =>
{
    // Method-group form — TEnvelope is inferred:
    options.ErrorEnvelope.Set(ErrorResponse.From);
});
```

The endpoint documents the error shape it actually produces:

```csharp
.MapGet("/{id}", ...)
.Produces<DefaultSuccessResponse<Order>>()
.Produces<ErrorResponse>(StatusCodes.Status404NotFound);
```

Everything — the envelope type, its per-request fields, and the mappings — is configured
once at `AddOutcomeHttp`. Skip `Set(...)` entirely and the default remains clean RFC 7807
`DefaultProblemDetails` (`title`/`status`/`detail`) when you configure nothing.

---

## Exception handling

Expected failures map to error envelopes automatically. **Unhandled exceptions** would
normally fall through to ASP.NET Core's default error page — a different shape. The
exception-handling middleware converts them into the **same envelope**, so every error is
consistent.

```csharp
builder.Services.AddOutcomeHttp(options => { /* ... */ });
builder.Services.AddOutcomeExceptionHandler(options =>
{
    // Map known exception types to HTTP INTENT (FailureDetails). The configured error
    // envelope renders it, so every exception shares the app's one error body. The error
    // code comes from the exception instance, not a hardcoded string:
    options.Exceptions.Map<DomainException>(ex =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, ex.Message, ex.Code));

    // FluentValidation → 400 with field errors. Per RFC 9457 each field maps to an
    // ARRAY of messages (one message is a single-element array):
    options.Exceptions.Map<ValidationException>(ex =>
        new FailureDetails(
            StatusCodes.Status400BadRequest,
            "Validation Error",
            code: "validation_error",
            errors: ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key,
                              g => g.Select(e => e.ErrorMessage).ToArray())));

    // Only surface UNHANDLED exception details in Development:
    options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
});

var app = builder.Build();
app.UseOutcomeExceptionHandler();   // BEFORE routing — catches downstream middleware too
```

### How mapping works

- **`options.Exceptions.Map<TException>`** behaves like `Failures.Map<TFailure>`: register
  any exception type (a subclass inherits its nearest registered base). The mapper returns
  HTTP intent (`FailureDetails`), which the configured error envelope renders — bypassing
  the generic 500 fallback and `IncludeExceptionDetails` gating. Per-request fields
  (`traceId`, …) come from the envelope's own factory.
- **Unmapped exceptions** fall through to the generic fallback:
  - Logged **in full** (with stack trace) via `ILogger`.
  - Rewritten as a clean RFC 7807 `DefaultProblemDetails` with status **500** — through
    `FromFailure`, so it shares the same error envelope and `Failures.Map` overrides as
    every other error.
  - `IncludeExceptionDetails = false` (default) returns a generic message and never leaks
    `ex.Message` to the client. It has **no effect** on mapped exceptions.
  - If the response has already started streaming, the exception is re-thrown rather than
    corrupting the output.

### What if you have your own exception handling — but it's not the final handler?

`UseOutcomeExceptionHandler` only renders the response — it doesn't stop you from persisting
exceptions elsewhere. To log an exception to your own store, wrap it with middleware that
sits **before** (outside) the exception handler, catches, records, then re-throws so the
handler still produces the envelope:

```csharp
// Register a service that writes the exception (DB, queue, ...) in DI, e.g.:
builder.Services.AddScoped<IExceptionLogService, ExceptionLogService>();

var app = builder.Build();

// 1. Your logging middleware FIRST (outermost) — catches, records, re-throws:
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        await context.RequestServices
            .GetRequiredService<IExceptionLogService>()
            .LogAsync(context, ex);
        throw;   // let the exception handler below produce the error envelope
    }
});

// 2. THEN the outcome exception handler (inner) — renders the same error envelope:
app.UseOutcomeExceptionHandler();
```

The key is **ordering**: your catch-all middleware must be registered **before**
`UseOutcomeExceptionHandler()`. Middleware runs outer-to-inner, so a middleware listed
first wraps everything after it — it sees the exception before the handler does. Putting
your logging middleware *after* the handler would mean it never sees exceptions thrown
inside endpoints, because the handler (outer) catches them first. Every exception —
mapped or unmapped — flows through your middleware and still ends up as the one
consistent error envelope.

---

## Pagination

Three types work together:

| Type | Role |
| --- | --- |
| `PageQuery` | Incoming `page` / `pageSize` parameters, bound from the query string |
| `PagedResult<T>` | Outgoing page from the service: `Items` + `TotalCount` |
| `PageInfo` | Response metadata: `Page`, `PageSize`, `TotalPages`, `TotalItems` |

**Service** returns a `PagedResult<T>` — the page/size live on the incoming `PageQuery`, so
the service only reports the raw `TotalCount`:

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
group.MapGet("/", (IOrderService service, [AsParameters] PageQuery pageQuery, IHttpResultFactory http) =>
    Outcome.Success(service.GetOrders(pageQuery)).ToHttpResult(http, page =>
        http.Ok(page.Items, PageInfo.Create(pageQuery, (int)page.TotalCount))));
```

- `[AsParameters]` binds each property from the query string (`?page=2&pageSize=20`).
- `PageQuery.Offset` gives you the 0-based offset directly — no more `(Page - 1) * PageSize`.
- `PageQuery` clamps `page` to `1..25` and `pageSize` to `1..50` by default, so a client
  cannot ask for an unbounded page. Out-of-range values are clamped, never silently reset —
  `?page=999` becomes page `25`.

> **Note:** `[AsParameters]` is a **framework feature** (Minimal API binding), available
> since **.NET 7**. The library itself does not depend on it — on .NET 6 you can bind
> `PageQuery` manually (e.g. `[FromQuery] int page`).

---

## JSON serialization options

The library writes error envelopes with `WriteAsJsonAsync`, which honors your app's JSON
serializer options. A best-practice setup — nulls omitted, camelCase, strict numbers, enums
as strings — is configured **separately for Minimal API and MVC**:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

// Minimal API — affects Results.* and WriteAsJsonAsync:
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// MVC (controllers) — configured separately:
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
```

Configured once, null properties are omitted everywhere — success envelopes, error
envelopes, and your own DTOs.

---

## Pluggable error shape

The library separates a failure's **intent** from the **shape** of the error envelope:

- `IHttpFailure.ToDetails()` (or a `Failures.Map` / `Exceptions.Map` mapper) declares
  intent — status code, message, code, field errors (`FailureDetails`).
- `ErrorEnvelopeOptions.Set<TEnvelope>` turns that intent into your concrete error body.

The default envelope is the clean RFC 7807 `DefaultProblemDetails` shown above. To change
the shape of *every* error response — typed, OpenAPI-documented, no `services.Replace` —
configure a typed envelope at `AddOutcomeHttp`:

```csharp
public sealed class ErrorResponse
{
    public int? StatusCode { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }
    public string? TraceId { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static ErrorResponse From(FailureDetails details, HttpContext context) => new()
    {
        StatusCode = details.StatusCode,
        Code = details.Code,
        Message = details.Message,
        TraceId = context.TraceIdentifier,
        Errors = details.Errors,
    };
}

builder.Services.AddOutcomeHttp(options =>
{
    options.ErrorEnvelope.Set(ErrorResponse.From);
});
```

Every error body is now `ErrorResponse`, documented exactly in OpenAPI via
`.Produces<ErrorResponse>()`:

```json
{
  "statusCode": 404,
  "code": "not_found",
  "message": "Order not found",
  "traceId": "0HLMS12B6V4H6:00000001"
}
```

The HTTP status is written from the failure's intent — your envelope is pure body, free to
be any type. No strategy interface, no string keys, no runtime injection.

---

## Testing

The library ships with a split test suite so you can mirror the same convention in your own
repos:

```
tests/
├── NebulaSpace.Outcome.Tests.Unit/                    # core: Outcome, Failure, Match, deconstruct
├── NebulaSpace.Outcome.Tests.Integration/             # core integration (placeholder)
├── NebulaSpace.Outcome.AspNetCore.Tests.Unit/         # in-memory: factories, envelopes, pagination
└── NebulaSpace.Outcome.AspNetCore.Tests.Integration/  # TestServer: exception middleware end-to-end
```

```bash
dotnet test
```

---

## Why `Outcome<T>` instead of exceptions?

- **Explicit contracts** — the failure is part of the method signature, not a side channel.
- **No `try/catch` for control flow** — expected failures are values, not exceptions.
- **Automatic HTTP mapping** — failures become correct status codes with one call.
- **Consistent errors** — expected failures and unhandled exceptions share one envelope.
- **Traceable responses** — opt-in `traceId` on every error envelope.

---

## Contributing

Contributions are welcome! Open an issue or a pull request.

- **Bug reports** — include the .NET version, package version, and a minimal repro.
- **Feature requests** — explain the use case; API changes are discussed before coding.
- **PRs** — keep the diff focused, add tests for new behavior, and ensure `dotnet build -warnaserror` and `dotnet test` pass.

---

## License

[MIT](LICENSE) © NebulaSpace

[outcome-shield]: https://img.shields.io/nuget/v/NebulaSpace.Outcome?style=flat-square&logo=nuget&logoColor=white&label=NebulaSpace.Outcome
[outcome-nuget]: https://www.nuget.org/packages/NebulaSpace.Outcome
[aspnetcore-shield]: https://img.shields.io/nuget/v/NebulaSpace.Outcome.AspNetCore?style=flat-square&logo=nuget&logoColor=white&label=NebulaSpace.Outcome.AspNetCore
[aspnetcore-nuget]: https://www.nuget.org/packages/NebulaSpace.Outcome.AspNetCore
[license-shield]: https://img.shields.io/badge/license-MIT-green.svg?style=flat-square
[license]: LICENSE
