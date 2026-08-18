<div align="center">

# NebulaSpace Outcome

**A tiny, dependency-free result pattern for .NET with a first-class ASP.NET Core integration that turns outcomes into clean HTTP responses.**

[![NuGet][outcome-shield]][outcome-nuget]
[![NuGet][aspnetcore-shield]][aspnetcore-nuget]
[![GitHub][github-shield]][github]
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
  - [Matching (the idiomatic handler)](#matching-the-idiomatic-handler)
  - [Pattern matching over outcomes](#pattern-matching-over-outcomes)
- [Failures](#failures)
  - [Built-in failures](#built-in-failures)
  - [Custom failures](#custom-failures)
  - [`FromFailure` resolution order](#fromfailure-resolution-order)
- [ASP.NET Core integration](#aspnet-core-integration)
  - [Register the services](#register-the-services)
  - [From outcome to response](#from-outcome-to-response)
  - [`IHttpResultFactory` helpers](#ihttpresultfactory-helpers)
  - [`Created` helpers](#created-helpers)
- [Response envelopes](#response-envelopes)
  - [Why define your own envelopes](#why-define-your-own-envelopes)
  - [Success envelope](#success-envelope)
  - [Swagger and typed data](#swagger-and-typed-data)
  - [Error envelope (RFC 7807 default)](#error-envelope-rfc-7807-default)
  - [Typed error envelope (recommended)](#typed-error-envelope-recommended)
- [Exception handling](#exception-handling)
  - [How mapping works](#how-mapping-works)
  - [Using your own exception handling alongside Outcome](#using-your-own-exception-handling-alongside-outcome)
- [Pagination](#pagination)
  - [Configuring limits](#configuring-limits)
  - [Offset pagination](#offset-pagination)
  - [Cursor pagination](#cursor-pagination)
  - [Custom pagination](#custom-pagination)
- [JSON serialization options](#json-serialization-options)
- [Testing](#testing)
- [Claude Code plugin](#claude-code-plugin)
- [Contributing](#contributing)
- [License](#license)

---

## Why Outcome?

Exceptions are for exceptional things. But most business errors ("not found", "duplicate",
"out of quota") are **expected outcomes**, not bugs. When you model them as exceptions you
pay the cost of `try/catch` control flow, lost compiler help, and a muddled contract.

`Outcome<T>` makes the failure part of the method signature. A service says exactly what it
can produce (a value, or a typed failure) and the caller is **forced** to handle both
branches. No hidden throw, no `.Result`, no guesswork.

```csharp
// Before: failure is invisible, caught nowhere in particular
public async Task<Order> CreateOrder(CreateOrderRequest request)
{
    if (await _repo.Exists(request.Product))
        throw new ConflictException("Product already exists");
    return await _repo.Save(new Order(request.Product));
}

// After: the contract is explicit, and the caller must handle both branches
public async Task<Outcome<Order>> CreateOrder(CreateOrderRequest request)
{
    if (await _repo.Exists(request.Product))
        return Outcome.Fail(new ConflictFailure("Product already exists"));
    return await _repo.Save(new Order(request.Product));   // implicit success
}
```

The ASP.NET Core integration closes the loop: `Outcome<T>` becomes `IResult` in one call,
with a consistent, configurable error envelope across expected failures and unhandled
exceptions.

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
            ? Outcome.Fail(new BadRequestFailure("Name cannot be empty."))
            : Outcome.Success($"Hello, {name}!");
}
```

**3. Register and map to an HTTP response**

```csharp
using NebulaSpace.Outcome.AspNetCore.Extensions;
using NebulaSpace.Outcome.AspNetCore.Http;

// Register once in Program.cs:
builder.Services.AddOutcome(options =>
{
    options.Success.SetResponse(typeof(SuccessResponse<>));    // non-paginated
    options.Success.SetResponse(typeof(SuccessResponse<,>));   // paginated
    options.Error.SetResponse<ErrorResponse>();
});

// Then in your endpoint:
app.MapGroup("/greet")
   .MapGet("/{name}", (string name, IGreetingService service, IHttpResultFactory http) =>
       service.Greet(name).ToHttpResult(http, greeting => http.Ok(new { greeting })));
```

Success invokes your handler; failure becomes the correct HTTP error automatically.
**No `try/catch` in the endpoint, ever.**

> The full runnable sample lives in [`samples/NebulaSpace.Outcome.Samples.WebApi`](samples/NebulaSpace.Outcome.Samples.WebApi).

---

## Packages

| Package | What's inside | Targets | Dependencies |
| --- | --- | --- | --- |
| `NebulaSpace.Outcome` | `Outcome`, `Outcome<T>`, `Failure`, `Unit` | **netstandard2.0** | none |
| `NebulaSpace.Outcome.AspNetCore` | `IHttpResultFactory`, options, failure types, pagination, exception handling | **net7.0** | core + `Microsoft.AspNetCore.App` (framework reference) |

Because NuGet picks the compatible asset, a `netstandard2.0` / `net7.0` package installs on
every later runtime (.NET 7, 8, 9, 10, and beyond). The **net7.0** floor enables
[static abstract interface members](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/static-abstract-interface-members)
(C# 11), which the typed success/error envelopes build on. The core package stays
`netstandard2.0` so it runs on .NET Framework 4.6.1+ as well.

---

## The core: `Outcome<T>`

A discriminated-union style value: either a **success** (with a `Value`) or a **failure**
(with a `Failure`). `Outcome<T>` is an **immutable `readonly struct`**. Its state is
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

### Matching (the idiomatic handler)

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
`Message` and an optional machine-readable `Code`. Provide a code only when one is meaningful:

```csharp
public abstract class Failure
{
    public string Message { get; }
    public string? Code { get; }             // optional, omit when there's no meaningful code

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

These ship in the **AspNetCore** package and know their own HTTP intent. No registration needed:

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

Three ways to give a failure an HTTP response, in increasing order of power:

**1. `options.Error.MapFailure<TFailure>`** registers a mapping at startup. The mapper returns
HTTP intent, which the configured error envelope renders:

```csharp
builder.Services.AddOutcome(options =>
{
    options.Error.MapFailure<QuotaExceededFailure>(f =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));
});
```

**2. Implement `IHttpFailure`** to declare the failure's HTTP intent on the type itself.
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

**3. A typed error envelope** changes the shape of *every* error response to your own
type, documented exactly in OpenAPI. See [Response envelopes](#response-envelopes).

### `FromFailure` resolution order

1. **`options.Error.MapFailure<TFailure>`** wins, even over built-in failures.
2. **`IHttpFailure`** maps itself.
3. **Unknown failures** degrade to **500 Internal Server Error** (never throws).

A subclass of a registered failure type inherits its nearest ancestor's mapping.

---

## ASP.NET Core integration

### Register the services

```csharp
builder.Services.AddOutcome(options =>
{
    // Success envelope: any open-generic type implementing ISuccessEnvelope<T>
    options.Success.SetResponse(typeof(SuccessResponse<>));    // non-paginated
    options.Success.SetResponse(typeof(SuccessResponse<,>));   // paginated

    // Error envelope: any type implementing IErrorEnvelope
    options.Error.SetResponse<ErrorResponse>();

    // Map application-defined failures to HTTP intent:
    options.Error.MapFailure<QuotaExceededFailure>(f =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));
});
```

`AddOutcome` registers `OutcomeOptions` and `IHttpResultFactory` in the DI container, so
**multiple apps in one process can configure the library independently**. The error shape is
configured here. No `services.Replace`, no strategy interface to swap.

### From outcome to response

`OutcomeHttpExtensions` (namespace `NebulaSpace.Outcome.AspNetCore.Http`) bridges outcomes
and `IResult`. Inject `IHttpResultFactory` into your endpoint or service and pass it along:

```csharp
// Handle both branches with one call:
result.ToHttpResult(http, order => http.Created(order, o => o.Id));

// Async services (Task<Outcome<T>>):
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
| `http.Created(data, locationUri)` | 201 with a manually specified `Location` |
| `http.NoContent()` | 204 |
| `http.BadRequest(...)` / `http.NotFound(...)` / `http.Conflict(...)` / ... | 4xx with the configured error envelope |
| `http.FromFailure(failure)` | Map any failure to its response |
| `http.FromFailureDetails(details)` | Build a response from HTTP intent (used by exception mappers) |

Because it is resolved through DI, the factory honors the options registered for *that*
application. No shared static state.

### `Created` helpers

`http.Created` builds the `Location` header for you. You only supply the new entity's id:

```csharp
// Before: you write the URI yourself
return Results.Created($"/orders/{order.Id}", order);

// After: the path comes from the request, only the id is supplied
return result.ToHttpResult(http, order => http.Created(order, o => o.Id));
// POST /orders        -> Location: /orders/{id}
// POST /admin/orders  -> Location: /admin/orders/{id}
```

The path is read from `HttpContext.Request.Path` when the response executes, so it can
never drift out of sync with the route that served it.

For cases where the resource URI doesn't follow the `{requestPath}/{id}` pattern (nested
routes, absolute URLs, cross-resource references), use the manual overload:

```csharp
return result.ToHttpResult(http, order => http.Created(order, $"/orders/{order.Id}/items/42"));
```

---

## Response envelopes

### Why define your own envelopes

> **You are strongly encouraged to define your own envelopes.**

The defaults are deliberately minimal: a clean RFC 7807 problem response for errors and a
thin `{ statusCode, data }` wrapper for successes. Real APIs usually want a consistent,
documented shape across the whole company: a `traceId` on every response, a stable `code`
for machine handling, a `requestId` echoed from the caller, a `version`, field-level errors
matching your API style guide. That is exactly what a typed envelope gives you:

- **Your type, serialized as-is.** The response body *is* `ErrorResponse` /
  `SuccessResponse<T>`, not a runtime dictionary the library merges fields into. OpenAPI
  documents the exact shape, typos are caught at compile time, and properties are real C#.
- **One consistent body everywhere.** Expected failures, `MapFailure` results, mapped
  exceptions, and unhandled exceptions all reduce to the same intent and render through
  your one envelope. Successes get wrapped in your one success envelope too.
- **Built once, fail-fast at startup.** Envelopes implement an interface with a static
  `From` factory. If a type doesn't implement it, `SetResponse` throws at startup, not on
  the first request that happens to hit an error.

Defining an envelope is ~10 lines. Strongly recommended for any real API.

### Success envelope

A success envelope is a type implementing `ISuccessEnvelope<T>`. Its static `From` builds
the per-request body from the payload, optional pagination metadata, and the current
request. Register it with `SetResponse(typeof(...))`. The library closes `T` per request
via `MakeGenericType` + cached `MethodInfo.Invoke` so Swagger sees the exact payload type:

```csharp
public sealed class SuccessResponse<TData> : ISuccessEnvelope<TData>
{
    public int StatusCode { get; init; }
    public TData? Data { get; init; }

    public static ISuccessEnvelope<TData> From(
        TData data, IPaginationDetails? pagination, HttpContext httpContext, int statusCode)
        => new SuccessResponse<TData>
        {
            StatusCode = statusCode,
            Data = data,
        };
}
```

```csharp
builder.Services.AddOutcome(options =>
{
    options.Success.SetResponse(typeof(SuccessResponse<>));
});
```

Every success becomes a `SuccessResponse<T>`. The endpoint documents it:

```csharp
.Produces<SuccessResponse<Order>>()                                  // GET /orders/{id}
.Produces<SuccessResponse<Order[]>>()                                // GET /orders
.Produces<SuccessResponse<Order>>(StatusCodes.Status201Created)      // POST /orders
```

When `Success.Enabled` is `false`, successes are returned raw with no wrapper and no
`statusCode`. Disable it with `options.Success.Enabled = false`.

### Swagger and typed data

The 1-param generic above already gives Swagger the exact `Data: Order` per endpoint. For
**paginated** responses, implement a **2-param** open-generic envelope so the second
parameter carries the concrete pagination type (`OffsetInfo` or `CursorInfo`):

```csharp
public sealed class SuccessResponse<TData, TPage> : ISuccessEnvelope<TData>
{
    public int StatusCode { get; init; }
    public TData? Data { get; init; }
    public TPage? Pagination { get; init; }

    public static ISuccessEnvelope<TData> From(
        TData data, IPaginationDetails? pagination, HttpContext httpContext, int statusCode)
        => new SuccessResponse<TData, TPage>
        {
            StatusCode = statusCode,
            Data = data,
            Pagination = pagination is TPage typed ? typed : default,
        };
}
```

```csharp
// NOT AOT-safe: uses MakeGenericType + cached MethodInfo.Invoke at runtime.
options.Success.SetResponse(typeof(SuccessResponse<>));   // non-paginated
options.Success.SetResponse(typeof(SuccessResponse<,>));  // paginated
.Produces<SuccessResponse<Order>>()                       // Swagger knows Data: Order
.Produces<SuccessResponse<Order[], OffsetInfo>>()         // Swagger knows Data + Pagination
```

> **AOT warning:** `SetResponse(typeof(...))` breaks NativeAOT. For AOT publishing, use
> the delegate overload `SetResponse(Func<...>)` instead. See `SuccessOptions` for details.
> The trade-off is Swagger shows `Data: object`.

### Error envelope (RFC 7807 default)

Error responses **always** use an envelope (regardless of `Success.Enabled`). If you
don't call `Error.SetResponse(...)`, the default is minimal RFC 7807
`DefaultProblemDetails` with only `title`, `status`, and `detail`:

```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Order not found"
}
```

No `type`, `traceId`, `path`, `errorCode`, or custom properties unless you configure them.
`DefaultProblemDetails` is non-nullable, so its OpenAPI schema is clean and `title`,
`status`, and `detail` are always present. `detail` is an empty string when the failure has
no message. The `title` comes from the status's standard reason phrase. Call
`Error.SetResponse(...)` only when you want a different error body, which is **recommended**.

### Typed error envelope (recommended)

`ErrorOptions.SetResponse<T>()` makes the error body **any class you define** that
implements `IErrorEnvelope`. The library serializes that type as-is and writes the HTTP
status from the failure's intent (`FailureDetails.StatusCode`):

- **OpenAPI documents the exact body.** `.Produces<ErrorResponse>(404)` describes
  precisely what the library writes.
- **Per-request fields are real C# properties.** `traceId`, `path`, `environment` are
  typed, not string keys in a runtime dictionary. Typos are caught at compile time.
- **One body everywhere.** Expected failures, `MapFailure` results, and mapped
  exceptions all reduce to a `FailureDetails`, which the envelope turns into this one shape.
- **Zero-reflection for errors.** `SetResponse<T>()` dispatches through
  `IErrorEnvelope.From` via `static abstract`, with no `GetMethod`/`Invoke` at all.

The library separates a failure's **intent** from the **shape** of the error body:

1. `IHttpFailure.ToDetails()` (or a `MapFailure` / `MapException` mapper) declares
   intent: status code, message, code, field errors (`FailureDetails`).
2. `ErrorOptions.SetResponse<T>()` turns that intent into your concrete error body,
   any type implementing `IErrorEnvelope`, with zero-reflection `static abstract` dispatch.

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
        Errors = details.Errors,   // RFC 9457: field name -> array of messages
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

The HTTP status is written from the failure's intent. Your envelope is pure body, free to
be any type. No strategy interface, no string keys, no runtime injection. Everything (the
envelope type, its per-request fields, and the mappings) is configured once at
`AddOutcome`. Skip `SetResponse(...)` entirely and the default remains clean RFC 7807
`DefaultProblemDetails`.

---

## Exception handling

Expected failures map to error envelopes automatically. **Unhandled exceptions** would
normally fall through to ASP.NET Core's default error page, a different shape. The
exception-handling middleware converts them into the **same envelope**, so every error is
consistent.

```csharp
builder.Services.AddOutcome(options =>
{
    // Success and error envelopes (configured above), plus exception mappers:
    options.Error.MapException<DomainException>(ex =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, ex.Message, ex.Code));

    // FluentValidation -> 400 with field errors. Per RFC 9457 each field maps to an
    // ARRAY of messages (one message is a single-element array):
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
app.UseOutcomeExceptionHandler();   // BEFORE routing, catches downstream middleware too
```

### How mapping works

- **`options.Error.MapException<TException>`** behaves like `MapFailure<TFailure>`: register
  any exception type (a subclass inherits its nearest registered base). The mapper returns
  HTTP intent (`FailureDetails`), which the configured error envelope renders, bypassing
  the generic 500 fallback and `IncludeExceptionDetails` gating. Per-request fields
  (`traceId`, etc.) come from the envelope's own factory.
- **Unmapped exceptions** fall through to the generic fallback:
  - Logged **in full** (with stack trace) via `ILogger`.
  - Rewritten as a clean RFC 7807 `DefaultProblemDetails` with status **500**, through
    `FromFailure`, so it shares the same error envelope as every other error.
  - `IncludeExceptionDetails = false` (default) returns a generic message and never leaks
    `ex.Message` to the client. It has **no effect** on mapped exceptions.
  - If the response has already started streaming, the exception is re-thrown rather than
    corrupting the output.

### Using your own exception handling alongside Outcome

`UseOutcomeExceptionHandler` only renders the response. It doesn't stop you from persisting
exceptions elsewhere. To log an exception to your own store, wrap it with middleware that
sits **before** (outside) the exception handler, catches, records, then re-throws so the
handler still produces the envelope:

```csharp
// Register a service that writes the exception (DB, queue, etc.) in DI:
builder.Services.AddScoped<IExceptionLogService, ExceptionLogService>();

var app = builder.Build();

// 1. Your logging middleware FIRST (outermost): catches, records, re-throws
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

// 2. THEN the outcome exception handler (inner): renders the same error envelope
app.UseOutcomeExceptionHandler();
```

The key is **ordering**: your catch-all middleware must be registered **before**
`UseOutcomeExceptionHandler()`. Middleware runs outer-to-inner, so a middleware listed
first wraps everything after it. It sees the exception before the handler does. Putting
your logging middleware *after* the handler would mean it never sees exceptions thrown
inside endpoints, because the handler (outer) catches them first. Every exception (mapped
or unmapped) flows through your middleware and still ends up as the one consistent error
envelope.

---

## Pagination

Both **offset** and **cursor** pagination are supported through a shared
`IPaginationDetails` marker interface. The pipeline works with either strategy via one
type. Custom implementations (keyset, time-based, etc.) are welcome. Implement
`IPaginationDetails` with whatever fields your strategy needs and pass it to
`http.Ok(data, myPagination)`.

### Configuring limits

`PaginationOptions`, set at startup, defines the maximum page and limit a client can
request. `AddOutcome` discovers every `IPaginatedQuery` implementor and calls its
`Configure` method to copy the options into the query type's static fields:

```csharp
builder.Services.AddOutcome(options =>
{
    options.Pagination.MaxPage = 25;    // max page number for offset
    options.Pagination.MaxLimit = 50;   // max items per page for both strategies
});
```

Default limits are `int.MaxValue` (effectively uncapped). Set values only when you want to
limit what clients can request.

The built-in `OffsetQuery` and `CursorQuery` already implement `IPaginatedQuery`. Custom
query types that implement the marker are picked up automatically. No wiring needed.

`MaxPage` and `MaxLimit` on the query types are static **fields** (not properties) to
avoid ASP.NET Core's `RequestDelegateFactory` trying to bind them as query parameters.
Clients can never inject their own limits through the query string.

### Offset pagination

Three types work together:

| Type | Role |
| --- | --- |
| `OffsetQuery` | Incoming `page` / `limit` parameters, bound from the query string |
| `OffsetResult<T>` | Outgoing page from the service: `Items` + `TotalCount` |
| `OffsetInfo` | Response metadata: `Page`, `Limit`, `TotalPages`, `TotalItems`, `HasMore` |

**Service** returns an `OffsetResult<T>`. The page/limit live on the incoming `OffsetQuery`,
so the service only reports the raw `TotalCount`:

```csharp
public OffsetResult<Order> GetOrders(OffsetQuery query)
{
    var items = _orders
        .Skip(query.Offset)          // (Page - 1) * Limit
        .Take(query.Limit)
        .ToArray();

    return new OffsetResult<Order> { Items = items, TotalCount = _orders.Count };
}
```

**Endpoint** binds `OffsetQuery` with `[AsParameters]` and maps the page into a response:

```csharp
group.MapGet("/", (IOrderService service, [AsParameters] OffsetQuery query, IHttpResultFactory http) =>
    Outcome.Success(service.GetOrders(query)).ToHttpResult(http, page =>
        http.Ok(page.Items, page.ToOffsetInfo(query))));
```

`?page=2&limit=20` produces:

```json
{
  "statusCode": 200,
  "data": [...],
  "pagination": {
    "page": 2,
    "limit": 20,
    "totalPages": 7,
    "totalItems": 137,
    "hasMore": true
  }
}
```

- `OffsetQuery.Offset` gives you the 0-based offset, no more `(Page - 1) * Limit`.
- Out-of-range values are clamped, never silently reset. `?page=999` becomes page `MaxPage`.
- `[AsParameters]` is a **framework feature** (Minimal API binding), available since
   **.NET 7**.

### Cursor pagination

| Type | Role |
| --- | --- |
| `CursorQuery` | Incoming `cursor` / `limit` parameters, bound from the query string |
| `CursorResult<T>` | Outgoing page from the service: `Items`, `PreviousCursor`, `NextCursor`, `TotalCount` |
| `CursorInfo` | Response metadata: `PreviousCursor`, `NextCursor`, `HasMore`, `TotalItems` |

**Service** returns a `CursorResult<T>`. Cursors are opaque strings; encoding/decoding is
the service's responsibility:

```csharp
public CursorResult<Order> GetOrdersCursor(CursorQuery cursorQuery)
{
    var startIndex = Decode(cursorQuery.Cursor); // null -> 0
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

`?limit=20` (first page) produces:

```json
{
  "statusCode": 200,
  "data": [...],
  "pagination": {
    "nextCursor": "eyJsYXN0SWQiOjIwfQ",
    "hasMore": true,
    "totalItems": 137
  }
}
```

- `previousCursor` is `null` on the first page (omitted from JSON).
- `nextCursor` is `null` on the last page (omitted from JSON), `hasMore` is `false`.
- `totalItems` may be `null` when the service doesn't compute the total count.
- `CursorQuery.Limit` is clamped to `1..MaxLimit`, same cap as offset, configurable via
  `PaginationOptions.MaxLimit`.

### Custom pagination

Any pagination strategy (keyset, time-based, composite) works out of the box. You need
three things: a query type, a result type, and a metadata type implementing
`IPaginationDetails`.

**1. Query type** implements `IPaginatedQuery` so `AddOutcome` configures its limits
automatically:

```csharp
public sealed class KeysetQuery : IPaginatedQuery
{
    public static int MaxLimit = 50;       // field, not property (RDE IL bug)

    private int _limit = 10;
    public string? AfterKey { get; set; }

    public int Limit
    {
        get => _limit;
        set => _limit = Math.Clamp(value, 1, MaxLimit);
    }

    public static void Configure(PaginationOptions options)
    {
        MaxLimit = Math.Max(1, options.MaxLimit);
    }
}
```

**2. Result type** is returned by the service, carries items + navigation keys:

```csharp
public sealed class KeysetResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public string? NextKey { get; set; }
    public string? PreviousKey { get; set; }
    public bool HasMore { get; set; }
    public long? TotalItems { get; set; }

    public KeysetInfo ToKeysetInfo() => new()
    {
        NextKey = NextKey,
        PreviousKey = PreviousKey,
        HasMore = HasMore,
        TotalItems = TotalItems,
    };
}
```

**3. Metadata type** implements `IPaginationDetails`, goes into the success envelope:

```csharp
public sealed class KeysetInfo : IPaginationDetails
{
    public string? NextKey { get; set; }
    public string? PreviousKey { get; set; }
    public bool HasMore { get; set; }
    public long? TotalItems { get; set; }
}
```

**Endpoint**, one line, same pattern as offset/cursor:

```csharp
group.MapGet("/keyset", (IProductService service, [AsParameters] KeysetQuery query, IHttpResultFactory http) =>
    Outcome.Success(service.GetProductsKeyset(query)).ToHttpResult(http, page =>
        http.Ok(page.Items, page.ToKeysetInfo())))
    .Produces<SuccessResponse<Product[], KeysetInfo>>();
```

`?limit=5` (first page) produces:

```json
{
  "statusCode": 200,
  "data": [...],
  "pagination": {
    "nextKey": "a1b2c3d4-...",
    "hasMore": true,
    "totalItems": 25
  }
}
```

No registration changes needed. The existing `SuccessResponse<,>` (2-param generic) closes
`TPage` with `KeysetInfo` at runtime via `MakeGenericType`. Custom query types that
implement `IPaginatedQuery` are discovered by `AddOutcome` automatically. No wiring.

---

## JSON serialization options

The library writes envelopes with `WriteAsJsonAsync`, which honors your app's JSON
serializer options. A best-practice setup (nulls omitted, camelCase, strict numbers, enums
as strings) is configured **separately for Minimal API and MVC**:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

// Minimal API: affects Results.* and WriteAsJsonAsync
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// MVC (controllers): configured separately
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
```

Configured once, null properties are omitted everywhere: success envelopes, error
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

## Claude Code plugin

This repo ships a [Claude Code](https://claude.ai/code) plugin with skills that give
Claude accurate, copy-pasteable guidance for using the library — no hallucinated API
shapes.

### Install

```
/plugin marketplace add nebulaspace-id/outcome-csharp
/plugin install outcome-tools@outcome-csharp
```

The first command registers this repo as a plugin marketplace; the second installs the
`outcome-tools` plugin into your local Claude Code config.

### What's inside

| Skill | Covers |
| --- | --- |
| `outcome-core` | `Outcome<T>`, `Failure`, `Unit`, matching, pattern matching, service method patterns |
| `outcome-aspnetcore` | `AddOutcome`, `UseOutcomeExceptionHandler`, `IHttpResultFactory`, response envelopes, `IHttpFailure`, `MapFailure`, `MapException`, pagination (offset, cursor, custom), endpoint wiring, JSON serialization |

Once installed, Claude automatically loads the relevant skill when you write code that
uses the library — creating outcomes, configuring DI, building paginated endpoints, etc.

### Team auto-config

To pre-register the marketplace for your team (so every contributor only needs the
install step), add this to `.claude/settings.json`:

```json
"extraKnownMarketplaces": {
  "outcome-csharp": {
    "source": { "source": "github", "repo": "nebulaspace-id/outcome-csharp" }
  }
}
```

---

## Contributing

Contributions are welcome! Open an issue or a pull request.

- **Bug reports**: include the .NET version, package version, and a minimal repro.
- **Feature requests**: explain the use case; API changes are discussed before coding.
- **PRs**: keep the diff focused, add tests for new behavior, and ensure `dotnet build -warnaserror` and `dotnet test` pass.

---

## License

[MIT](LICENSE) © NebulaSpace

[outcome-shield]: https://img.shields.io/nuget/v/NebulaSpace.Outcome?style=flat-square&logo=nuget&logoColor=white&label=NebulaSpace.Outcome
[outcome-nuget]: https://www.nuget.org/packages/NebulaSpace.Outcome
[aspnetcore-shield]: https://img.shields.io/nuget/v/NebulaSpace.Outcome.AspNetCore?style=flat-square&logo=nuget&logoColor=white&label=NebulaSpace.Outcome.AspNetCore
[aspnetcore-nuget]: https://www.nuget.org/packages/NebulaSpace.Outcome.AspNetCore
[github-shield]: https://img.shields.io/badge/GitHub-repo-black?style=flat-square&logo=github&logoColor=white
[github]: https://github.com/nebulaspace-id/outcome-csharp
[license-shield]: https://img.shields.io/badge/license-MIT-green.svg?style=flat-square
[license]: LICENSE
