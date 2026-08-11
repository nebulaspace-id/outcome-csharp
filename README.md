<div align="center">

# NebulaSpace Outcome

**A tiny, dependency-free result pattern for .NET — with a first-class ASP.NET Core integration that turns outcomes into clean HTTP responses.**

[![NuGet][outcome-shield]][outcome-nuget]
[![NuGet][aspnetcore-shield]][aspnetcore-nuget]
[![MIT License][license-shield]][license]

`Outcome<T>` · `Failure` · `IHttpResultFactory` · Success/Error envelopes · exception mapping

</div>

---

## Table of contents

- [Why Outcome?](#why-outcome)
- [Quick start](#quick-start)
- [Packages](#packages)
- [The core: `Outcome<T>`](#the-core-outcomet)
  - [Creating outcomes](#creating-outcomes)
  - [Reading the state](#reading-the-state)
  - [Matching — the idiomatic handler](#matching--the-idiomatic-handler)
  - [Pattern matching over outcomes](#pattern-matching-over-outcomes)
- [Failures](#failures)
  - [Built-in failures](#built-in-failures)
  - [Custom failures](#custom-failures)
  - [`FromFailure` resolution order](#fromfailure-resolution-order)
- [ASP.NET Core integration](#aspnet-core-integration)
  - [Register the services](#register-the-services)
  - [From outcome to response](#from-outcome-to-response)
  - [`IHttpResultFactory` helpers](#ihttpresultfactory-helpers)
  - [`Created` — no manual URI writing](#created--no-manual-uri-writing)
- [Response envelopes](#response-envelopes)
  - [Why define your own envelopes](#why-define-your-own-envelopes)
  - [Success envelope (AOT‑safe)](#success-envelope-aotsafe)
  - [Swagger & typed <c>data</c>](#swagger--typed-data)
  - [Error envelope — clean RFC 7807 by default](#error-envelope--clean-rfc-7807-by-default)
  - [Typed error envelope (recommended)](#typed-error-envelope-recommended)
- [Exception handling](#exception-handling)
  - [How mapping works](#how-mapping-works)
  - [What if you have your own exception handling — but it's not the final handler?](#what-if-you-have-your-own-exception-handling--but-its-not-the-final-handler)
- [Pagination](#pagination)
- [JSON serialization options](#json-serialization-options)
- [Testing](#testing)
- [Contributing](#contributing)
- [License](#license)

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

**3. Register and map to an HTTP response**

```csharp
using NebulaSpace.Outcome.AspNetCore.Extensions;
using NebulaSpace.Outcome.AspNetCore.Http;

// Register once in Program.cs:
builder.Services.AddOutcome(options =>
{
    options.Success.SetResponse<SuccessResponse>();
    options.Error.SetResponse<ErrorResponse>();
});

// Then in your endpoint:
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
| `NebulaSpace.Outcome.AspNetCore` | `IHttpResultFactory`, options, failure types, pagination, exception handling | **net7.0** | core + `Microsoft.AspNetCore.App` (framework reference) |

Because NuGet picks the compatible asset, a `netstandard2.0` / `net7.0` package installs on
**every later runtime** — .NET 7, 8, 9, 10, and beyond. The **net7.0** floor enables
[static abstract interface members](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/static-abstract-interface-members)
(C# 11), which the typed success/error envelopes build on. The core package stays
`netstandard2.0` so it runs on .NET Framework 4.6.1+ as well.

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

**1. `options.Error.MapFailure<TFailure>`** — register a mapping at startup. The mapper returns
HTTP intent, which the configured error envelope renders:

```csharp
builder.Services.AddOutcome(options =>
{
    options.Error.MapFailure<QuotaExceededFailure>(f =>
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
type, documented exactly in OpenAPI. See [Response envelopes](#response-envelopes).

### `FromFailure` resolution order

1. **`options.Error.MapFailure<TFailure>`** — an explicit registration wins, even over built-in failures.
2. **`IHttpFailure`** — the failure maps itself.
3. **Unknown failures** — degrade to **500 Internal Server Error** (never throws).

A subclass of a registered failure type inherits its nearest ancestor's mapping.

---

## ASP.NET Core integration

### Register the services

```csharp
builder.Services.AddOutcome(options =>
{
    // Success envelope — any type implementing ISuccessEnvelope, AOT-safe, zero-reflection:
    options.Success.SetResponse<SuccessResponse>();

    // Error envelope — any type implementing IErrorEnvelope, AOT-safe, zero-reflection:
    options.Error.SetResponse<ErrorResponse>();

    // Map application-defined failures to HTTP intent:
    options.Error.MapFailure<QuotaExceededFailure>(f =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));
});
```

`AddOutcome` registers `OutcomeOptions` and `IHttpResultFactory` in the DI
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
| `http.Ok(data)` | 200, raw or wrapped per `Success.Enabled` |
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

### Why define your own envelopes

> **You are strongly encouraged to define your own envelopes.**

The defaults are deliberately minimal — a clean RFC 7807 problem response for errors and a
thin `{ statusCode, data }` wrapper for successes. Real APIs usually want a consistent,
documented shape across the whole company: a `traceId` on every response, a stable `code`
for machine handling, a `requestId` echoed from the caller, a `version`, field-level errors
matching your API style guide. That is exactly what a typed envelope gives you:

- **Your type, serialized as-is.** The response body *is* `ErrorResponse` /
  `SuccessResponse<T>` — not a runtime dictionary the library merges fields into. OpenAPI
  documents the exact shape, typos are caught at compile time, and properties are real C#.
- **One consistent body everywhere.** Expected failures, `MapFailure` results, mapped
  exceptions, and unhandled exceptions all reduce to the same intent and render through
  your one envelope. Successes get wrapped in your one success envelope too.
- **Built once, fail-fast at startup.** Envelopes implement an interface with a static
  `From` factory. If a type doesn't implement it, `SetResponse` throws at startup — not on the
  first request that happens to hit an error.

Defining an envelope is ~10 lines. Strongly recommended for any real API.

### Success envelope (AOT‑safe)

A success envelope is a type implementing `ISuccessEnvelope`. Its static `From` builds the
per‑request body from the response intent (`SuccessDetails`) and the current request.
Register it with the generic `<c>SetResponse&lt;T&gt;</c>` — compile‑time safe, AOT‑ready,
zero‑reflection via `static abstract` dispatch:

```csharp
public sealed class SuccessResponse : ISuccessEnvelope
{
    public int StatusCode { get; init; }
    public object? Data { get; init; }
    public IPageMetadata? Pagination { get; init; }
    public string? TraceId { get; init; }

    public static ISuccessEnvelope From(SuccessDetails details, HttpContext http) => new SuccessResponse
    {
        StatusCode = details.StatusCode,
        Data = details.Data,
        Pagination = details.Pagination,
        TraceId = http.TraceIdentifier,
    };
}
```

```csharp
builder.Services.AddOutcome(options =>
{
    options.Success.SetResponse<SuccessResponse>();
});
```

Every success becomes a `SuccessResponse`. The endpoint documents it:

```csharp
.Produces<SuccessResponse>()                                  // GET /orders/{id}
.Produces<SuccessResponse>()                                  // GET /orders
.Produces<SuccessResponse>(StatusCodes.Status201Created)      // POST /orders
```

When `Success.Enabled` is `false`, successes are returned raw — no wrapper, no `statusCode`.
Disable it with `options.Success.Enabled = false`.

### Swagger & typed <c>data</c>

The non‑generic `ISuccessEnvelope` above shows `Data` as `object` in OpenAPI. For
per‑endpoint typed `Data: Order`, implement `ISuccessEnvelope<T>` on an **open‑generic**
type and register it with `SetResponse(typeof(…))`. The library closes `T` per request via
`MakeGenericType` + cached `MethodInfo.Invoke` so Swagger sees the exact payload type:

```csharp
public sealed class SuccessResponse<T> : ISuccessEnvelope<T>
{
    public int StatusCode { get; init; }
    public T? Data { get; init; }
    public IPageMetadata? Pagination { get; init; }

    public static ISuccessEnvelope<T> From(
        T data, IPageMetadata? pagination, HttpContext http, int statusCode)
        => new SuccessResponse<T>
        {
            StatusCode = statusCode,
            Data = data,
            Pagination = pagination,
        };
}
```

```csharp
// NOT AOT‑safe — uses MakeGenericType + cached MethodInfo.Invoke at runtime.
options.Success.SetResponse(typeof(SuccessResponse<>));
.Produces<SuccessResponse<Order>>()      // Swagger knows Data: Order
```

> **AOT warning:** <c>SetResponse(typeof(…))</c> breaks NativeAOT. For AOT publishing, use
> the non‑generic <c>SetResponse&lt;SuccessResponse&gt;()</c> above instead — the only
> trade‑off is Swagger shows `Data: object`.

### Error envelope — clean RFC 7807 by default

Error responses **always** use an envelope (regardless of `Success.Enabled`). If you
don't call `Error.SetResponse(...)`, the default is minimal RFC 7807
`DefaultProblemDetails` — only `title`, `status`, and `detail`:

```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Order not found"
}
```

No `type`, `traceId`, `path`, `errorCode`, or custom properties unless you configure them.
`DefaultProblemDetails` is non‑nullable, so its OpenAPI schema is clean and `title`,
`status`, and `detail` are always present — `detail` is an empty string when the failure has
no message. The `title` comes from the status's standard reason phrase. Call
`Error.SetResponse(...)` only when you want a different error body — which, again, is
**recommended**.

### Typed error envelope (recommended)

`ErrorOptions.SetResponse<T>()` makes the error body **any class you define** that
implements `IErrorEnvelope`. The library serializes that type as‑is and writes the HTTP
status from the failure's intent (`FailureDetails.StatusCode`):

- **OpenAPI documents the exact body** — `.Produces<ErrorResponse>(404)` describes
  precisely what the library writes.
- **Per‑request fields are real C# properties** — `traceId`, `path`, `environment` are
  typed, not string keys in a runtime dictionary. Typos are caught at compile time.
- **One body everywhere** — expected failures, `MapFailure` results, and mapped
  exceptions all reduce to a `FailureDetails`, which the envelope turns into this one shape.
- **AOT‑safe, zero‑reflection** — the generic <c>SetResponse&lt;T&gt;()</c> dispatches through
  `IErrorEnvelope.From` via `static abstract`, with no `GetMethod`/`Invoke` at all.

The library separates a failure's **intent** from the **shape** of the error body:

1. `IHttpFailure.ToDetails()` (or a `MapFailure` / `MapException` mapper) declares
   intent — status code, message, code, field errors (`FailureDetails`).
2. `ErrorOptions.SetResponse<T>()` turns that intent into your concrete error body —
   any type implementing `IErrorEnvelope`, with zero‑reflection `static abstract` dispatch.

```csharp
public sealed class ErrorResponse : IErrorEnvelope
{
    public int? StatusCode { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }
    public string? TraceId { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static IErrorEnvelope From(FailureDetails details, HttpContext context) => new ErrorResponse
    {
        StatusCode = details.StatusCode,
        Code = details.Code,
        Message = details.Message,
        TraceId = context.TraceIdentifier,
        Errors = details.Errors,   // RFC 9457: field name → array of messages
    };
}

builder.Services.AddOutcome(options =>
{
    options.Error.SetResponse<ErrorResponse>();
});
```

The endpoint documents the error shape it actually produces:

```csharp
.MapGet("/{id}", ...)
.Produces<SuccessResponse<Order>>()
.Produces<ErrorResponse>(StatusCodes.Status404NotFound);
```

Every error body is now `ErrorResponse`:

```json
{
  "statusCode": 404,
  "code": "not_found",
  "message": "Order not found",
  "traceId": "0HLMS12B6V4H6:00000001"
}
```

The HTTP status is written from the failure's intent — your envelope is pure body, free to
be any type. No strategy interface, no string keys, no runtime injection. Everything — the
envelope type, its per‑request fields, and the mappings — is configured once at
`AddOutcome`. Skip <c>SetResponse(...)</c> entirely and the default remains clean RFC 7807
`DefaultProblemDetails`.

---

## Exception handling

Expected failures map to error envelopes automatically. **Unhandled exceptions** would
normally fall through to ASP.NET Core's default error page — a different shape. The
exception‑handling middleware converts them into the **same envelope**, so every error is
consistent.

```csharp
builder.Services.AddOutcome(options =>
{
    // Success and error envelopes (configured above), plus exception mappers:
    options.Error.MapException<DomainException>(ex =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, ex.Message, ex.Code));

    // FluentValidation → 400 with field errors. Per RFC 9457 each field maps to an
    // ARRAY of messages (one message is a single‑element array):
    options.Error.MapException<ValidationException>(ex =>
        new FailureDetails(
            StatusCodes.Status400BadRequest,
            "Validation Error",
            code: "validation_error",
            errors: ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key,
                              g => g.Select(e => e.ErrorMessage).ToArray())));

    // Only surface UNHANDLED exception details in Development:
    options.Error.IncludeExceptionDetails = builder.Environment.IsDevelopment();
});

var app = builder.Build();
app.UseOutcomeExceptionHandler();   // BEFORE routing — catches downstream middleware too
```

### How mapping works

- **`options.Error.MapException<TException>`** behaves like `MapFailure<TFailure>`: register
  any exception type (a subclass inherits its nearest registered base). The mapper returns
  HTTP intent (`FailureDetails`), which the configured error envelope renders — bypassing
  the generic 500 fallback and `IncludeExceptionDetails` gating. Per‑request fields
  (`traceId`, …) come from the envelope's own factory.
- **Unmapped exceptions** fall through to the generic fallback:
  - Logged **in full** (with stack trace) via `ILogger`.
  - Rewritten as a clean RFC 7807 `DefaultProblemDetails` with status **500** — through
    `FromFailure`, so it shares the same error envelope as every other error.
  - `IncludeExceptionDetails = false` (default) returns a generic message and never leaks
    `ex.Message` to the client. It has **no effect** on mapped exceptions.
  - If the response has already started streaming, the exception is re‑thrown rather than
    corrupting the output.

### What if you have your own exception handling — but it's not the final handler?

`UseOutcomeExceptionHandler` only renders the response — it doesn't stop you from persisting
exceptions elsewhere. To log an exception to your own store, wrap it with middleware that
sits **before** (outside) the exception handler, catches, records, then re‑throws so the
handler still produces the envelope:

```csharp
// Register a service that writes the exception (DB, queue, ...) in DI, e.g.:
builder.Services.AddScoped<IExceptionLogService, ExceptionLogService>();

var app = builder.Build();

// 1. Your logging middleware FIRST (outermost) — catches, records, re‑throws:
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

The key is **ordering**: your catch‑all middleware must be registered **before**
`UseOutcomeExceptionHandler()`. Middleware runs outer‑to‑inner, so a middleware listed
first wraps everything after it — it sees the exception before the handler does. Putting
your logging middleware *after* the handler would mean it never sees exceptions thrown
inside endpoints, because the handler (outer) catches them first. Every exception —
mapped or unmapped — flows through your middleware and still ends up as the one
consistent error envelope.

---

## Pagination

Both **offset** and **cursor** pagination are supported through a shared
`IPageMetadata` interface — the pipeline works with either strategy via one type. The
`Type` discriminator (`"offset"` / `"cursor"`) lets clients decide how to render
pagination without sniffing which fields are non‑null.

### Configuring caps

`PaginationOptions` — set at startup — defines the maximum page and limit a client can
request. These are copied to static defaults on `PageQuery` and `CursorQuery` so
`[AsParameters]` binding picks them up:

```csharp
builder.Services.AddOutcome(options =>
{
    options.Pagination.MaxPage = 25;    // max page number for offset
    options.Pagination.MaxLimit = 50;   // max items per page for both strategies
});
```

`MaxPage` and `MaxLimit` on the query types carry `[BindNever]` — clients can **never**
inject their own caps through the query string.

### Offset pagination

Three types work together:

| Type | Role |
| --- | --- |
| `PageQuery` | Incoming `page` / `limit` parameters, bound from the query string |
| `PageResult<T>` | Outgoing page from the service: `Items` + `TotalCount` |
| `PageInfo` | Response metadata: `type: "offset"`, `Page`, `Limit`, `TotalPages`, `TotalItems`, `HasMore` |

**Service** returns a `PageResult<T>` — the page/limit live on the incoming `PageQuery`, so
the service only reports the raw `TotalCount`:

```csharp
public PageResult<Order> GetOrders(PageQuery pageQuery)
{
    var items = _orders
        .Skip(pageQuery.Offset)          // (Page - 1) * Limit
        .Take(pageQuery.Limit)
        .ToArray();

    return new PageResult<Order> { Items = items, TotalCount = _orders.Count };
}
```

**Endpoint** binds `PageQuery` with `[AsParameters]` and maps the page into a response:

```csharp
group.MapGet("/", (IOrderService service, [AsParameters] PageQuery pageQuery, IHttpResultFactory http) =>
    Outcome.Success(service.GetOrders(pageQuery)).ToHttpResult(http, page =>
        http.Ok(page.Items, PageInfo.Create(pageQuery, (int)page.TotalCount))));
```

`?page=2&limit=20` → the JSON payload:

```json
{
  "statusCode": 200,
  "data": [...],
  "pagination": {
    "type": "offset",
    "page": 2,
    "limit": 20,
    "totalPages": 7,
    "totalItems": 137,
    "hasMore": true
  }
}
```

- `PageQuery.Offset` gives you the 0‑based offset — no more `(Page - 1) * Limit`.
- Out‑of‑range values are clamped, never silently reset — `?page=999` becomes page `MaxPage`.
- `[AsParameters]` is a **framework feature** (Minimal API binding), available since
   **.NET 7**.

### Cursor pagination

| Type | Role |
| --- | --- |
| `CursorQuery` | Incoming `cursor` / `limit` parameters, bound from the query string |
| `CursorResult<T>` | Outgoing page from the service: `Items`, `PreviousCursor`, `NextCursor`, `TotalCount` |
| `CursorInfo` | Response metadata: `type: "cursor"`, `PreviousCursor`, `NextCursor`, `HasMore`, `TotalItems` |

**Service** returns a `CursorResult<T>` — cursors are opaque strings; encoding/decoding is
the service's responsibility:

```csharp
public CursorResult<Order> GetOrdersCursor(CursorQuery cursorQuery)
{
    var startIndex = Decode(cursorQuery.Cursor); // null → 0
    var items = _orders.Skip(startIndex).Take(cursorQuery.Limit).ToArray();
    var endIndex = startIndex + items.Length;

    return new CursorResult<Order>
    {
        Items = items,
        PreviousCursor = startIndex == 0 ? null : Encode(Math.Max(0, startIndex - cursorQuery.Limit)),
        NextCursor = endIndex < _orders.Count ? Encode(endIndex) : null,
        TotalCount = _orders.Count,
    };
}
```

**Endpoint** binds `CursorQuery` with `[AsParameters]`:

```csharp
group.MapGet("/cursor", (IOrderService service, [AsParameters] CursorQuery cursorQuery, IHttpResultFactory http) =>
    Outcome.Success(service.GetOrdersCursor(cursorQuery)).ToHttpResult(http, page =>
        http.Ok(page.Items, CursorInfo.Create(cursorQuery, page))));
```

`?limit=20` (first page) → the JSON payload:

```json
{
  "statusCode": 200,
  "data": [...],
  "pagination": {
    "type": "cursor",
    "nextCursor": "eyJsYXN0SWQiOjIwfQ",
    "hasMore": true,
    "totalItems": 137
  }
}
```

- `previousCursor` is `null` on the first page (omitted from JSON).
- `nextCursor` is `null` on the last page (omitted from JSON), `hasMore` is `false`.
- `totalItems` may be `null` when the service doesn't compute the total count.
- `CursorQuery.Limit` is clamped to `1..MaxLimit` — same cap as offset, configurable via
  `PaginationOptions.MaxLimit`.

---

## JSON serialization options

The library writes envelopes with `WriteAsJsonAsync`, which honors your app's JSON
serializer options. A best‑practice setup — nulls omitted, camelCase, strict numbers, enums
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
