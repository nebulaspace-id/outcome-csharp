---
name: outcome-aspnetcore
description: >
  Use when configuring or using NebulaSpace.Outcome.AspNetCore:
  AddOutcome, UseOutcomeExceptionHandler, IHttpResultFactory, ToResult,
  response envelopes, failure/exception mapping, pagination, endpoint wiring,
  or JSON serialization.
license: MIT
---

# NebulaSpace.Outcome — ASP.NET Core Integration

## Installation

```bash
dotnet add package NebulaSpace.Outcome
dotnet add package NebulaSpace.Outcome.AspNetCore
```

The core package targets **netstandard2.0** (no dependencies). The ASP.NET Core package targets **net7.0+**.

---

## Register Services

In `Program.cs`:

```csharp
using NebulaSpace.Outcome.AspNetCore.Extensions;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

builder.Services.AddOutcome(options =>
{
    // Success envelopes
    options.Success.SetResponse(typeof(SuccessResponse<>));    // non-paginated (open-generic)
    options.Success.SetResponse(typeof(SuccessResponse<,>));   // paginated (open-generic)
    options.Success.SetResponse(typeof(SuccessResponse));      // no-payload (non-generic, http.Ok())

    // Error envelope
    options.Error.SetResponse<ErrorResponse>();

    // Pagination caps
    options.Pagination.MaxPage = 25;
    options.Pagination.MaxLimit = 50;

    // Map app-defined failures to HTTP intent
    options.Error.MapFailure<QuotaExceededFailure>(f =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));

    // Map exceptions to HTTP intent
    options.Error.MapException<DomainException>(ex =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, ex.Message, "domain_error"));

    // FluentValidation → 400 with field errors (RFC 9457)
    options.Error.MapException<ValidationException>(ex =>
        new FailureDetails(
            StatusCodes.Status400BadRequest,
            message: "Validation Error",
            code: "validation_error",
            errors: ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));

    // Only leak unhandled exception details in Development
    options.Error.IncludeExceptionDetails = builder.Environment.IsDevelopment();
});
```

`AddOutcome` registers `OutcomeOptions` (singleton), `IHttpResultFactory` (singleton), and the exception-handling middleware (transient). Calling it twice throws.

> **AOT:** `SetResponse(typeof(...))` uses `MakeGenericType` and is not NativeAOT-safe.
> For AOT, register envelopes with the delegate overloads and the generic
> `SetResponse<TEnvelope>()`, and register pagination query types explicitly — no
> reflection at runtime:
>
> ```csharp
> builder.Services.AddOutcome(options =>
> {
>     // Success envelope — delegate overload; handles paginated responses too
>     // (pagination arrives as the second argument). Data/Pagination become `object` in Swagger.
>     options.Success.SetResponse((data, pagination, http, statusCode) =>
>         new SuccessResponse { StatusCode = statusCode, Data = data, Pagination = pagination });
>
>     // No-payload success (http.Ok() with no args) — delegate overload
>     options.Success.SetResponse((http, statusCode) =>
>         new SuccessResponse { StatusCode = statusCode });
>
>     // Error envelope — generic form dispatches via static abstract, AOT-safe
>     options.Error.SetResponse<ErrorResponse>();
>
>     // Pagination — disable the reflection-based assembly scan and register each query
>     // type explicitly (built-in Default* included, if used)
>     options.Pagination.ScanAssemblies = false;
>     options.Pagination.RegisterQuery<MyKeysetQuery>();
>     options.Pagination.RegisterQuery<DefaultOffsetQuery>();
>     options.Pagination.RegisterQuery<DefaultCursorQuery>();
> });
> ```
>
> The envelope's `Data`/`Pagination` properties must be typed `object`, not a concrete type
> or `IPaginationDetails`: STJ serializes an interface-typed property using the interface
> contract (empty `{}` for a marker interface), while object-typed properties serialize by
> runtime type — the JSON keeps its full shape. Only the Swagger schema becomes generic.

## Register Middleware

```csharp
var app = builder.Build();

app.UseOutcomeExceptionHandler();   // BEFORE routing
app.UseHttpsRedirection();
app.MapProductEndpoints();
app.Run();
```

### Ordering with Custom Logging Middleware

```csharp
// Your logging middleware FIRST (outermost) — catches, records, re-throws
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        await context.RequestServices
            .GetRequiredService<IExceptionLogService>()
            .LogAsync(context, ex);
        throw;
    }
});

app.UseOutcomeExceptionHandler();   // THEN the handler — renders the error envelope
```

---

## Inject `IHttpResultFactory`

Singleton lifetime, inject into endpoints or services:

```csharp
group.MapGet("/{id}", (Guid id, IOrderService service, IHttpResultFactory http) =>
    service.GetOrder(id).ToResult(http, order => http.Ok(order)));
```

## Extension Methods

| Method | Use when |
| --- | --- |
| `outcome.ToResult(http, onSuccess)` | Sync service returning `Outcome<T>` |
| `await outcomeTask.ToResultAsync(http, onSuccess)` | Async service returning `Task<Outcome<T>>` |
| `await outcomeTask.ToResultAsync(http, async onSuccess)` | Async service with async success handler |
| `failure.ToErrorResponse(http)` | You already have the `Failure` object |

## `IHttpResultFactory` Helpers

| Helper | Behavior |
| --- | --- |
| `http.Ok()` | 200 with no payload (status-only envelope) |
| `http.Ok(data)` | 200, raw or wrapped per `Success.Enabled` |
| `http.Ok(data, pagination)` | 200 with pagination metadata |
| `http.Created(data, idSelector)` | 201 with `Location` derived from request path |
| `http.Created(data, locationUri)` | 201 with a manually specified `Location` |
| `http.NoContent()` | 204 |
| `http.BadRequest(...)` / `http.NotFound(...)` / `http.Conflict(...)` / ... | 4xx with the configured error envelope |
| `http.FromFailure(failure)` | Map any failure to its response |
| `http.FromFailureDetails(details)` | Build a response from HTTP intent |

### `Created` Helpers

```csharp
// Automatic Location: POST /orders -> Location: /orders/{id}
return result.ToResult(http, order => http.Created(order, o => o.Id));

// Manual Location: for non-standard URIs
return result.ToResult(http, order => http.Created(order, $"/orders/{order.Id}/items/42"));
```

---

## Built-in Failures

These ship in the package, implement `IHttpFailure`, and need no registration:

| Failure | HTTP status | Code |
| --- | --- | --- |
| `BadRequestFailure` | 400 | `bad_request` |
| `UnauthorizedFailure` | 401 | `unauthorized` |
| `ForbiddenFailure` | 403 | `forbidden` |
| `NotFoundFailure` | 404 | `not_found` |
| `ConflictFailure` | 409 | `conflict` |
| `ValidationFailure` | 400 | `validation` |
| `InternalServerErrorFailure` | 500 | `internal_server_error` |

`BadRequestFailure` and `ValidationFailure` accept an optional `IReadOnlyDictionary<string, string[]>? errors` for field-level validation (RFC 9457).

---

## Endpoint Patterns

```csharp
var group = app.MapGroup("/products");

// GET with no payload
group.MapGet("/ping", (IHttpResultFactory http) => http.Ok())
    .Produces<SuccessResponse>();

// GET with 404
group.MapGet("/{id:guid}", (Guid id, IProductService service, IHttpResultFactory http) =>
    service.GetProduct(id).ToResult(http, product => http.Ok(product)))
    .Produces<SuccessResponse<Product>>()
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

// POST with 201
group.MapPost("/", (CreateProductRequest req, IProductService service, IHttpResultFactory http) =>
    service.CreateProduct(req).ToResultAsync(http, product => http.Created(product, p => p.Id)))
    .Produces<SuccessResponse<Product>>(StatusCodes.Status201Created)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

// DELETE with 204
group.MapDelete("/{id:guid}", (Guid id, IProductService service, IHttpResultFactory http) =>
    service.DeleteProduct(id).ToResult(http, _ => http.NoContent()))
    .Produces(StatusCodes.Status204NoContent)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound);
```

---

## MVC Controllers

Two patterns for MVC controllers — both reuse the same `IHttpResultFactory`, envelopes, and failure mapping.

### Pattern 1: `[OutcomeResult]` filter

Return `Outcome<T>` directly from an action. The filter converts it to the correct HTTP response:

```csharp
[ApiController, Route("products")]
[OutcomeResult]                            // per-controller or global
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;
    public ProductsController(IProductService service) => _service = service;

    [HttpGet("ping")]
    public IActionResult Ping() => _http.Ok().ToActionResult();

    [HttpGet("{id:guid}")]
    public Outcome<Product> Get(Guid id) => _service.GetProduct(id);   // 200 or 404

    [HttpDelete("{id:guid}")]
    public Outcome<Unit> Delete(Guid id) => _service.DeleteProduct(id); // 204 or 404
}
```

### Pattern 2: explicit `ToActionResult` / `ToActionResultAsync`

For custom success responses (201 with Location, pagination metadata, etc.):

```csharp
[HttpPost]
public Task<IActionResult> Create([FromBody] CreateProductRequest request) =>
    _service.CreateProduct(request)
        .ToActionResultAsync(_http, product => _http.Created(product, p => p.Id));

[HttpGet]
public IActionResult List([FromQuery] OffsetQuery query)
{
    var result = _service.GetProducts(query);
    return Outcome.Success(result.Items)
        .ToActionResult(_http, items => _http.Ok(items, result.ToOffsetInfo(query)));
}
```

### MVC Extension Methods

| Method | Use when |
| --- | --- |
| `outcome.ToActionResult(factory)` | Sync, auto-maps success/failure |
| `outcome.ToActionResult(factory, onSuccess)` | Sync with custom success logic |
| `await outcomeTask.ToActionResultAsync(factory)` | Async, auto-maps |
| `await outcomeTask.ToActionResultAsync(factory, onSuccess)` | Async with custom success |
| `result.ToActionResult()` | Wrap an `IResult` as `IActionResult` |

### Register

```csharp
builder.Services.AddControllers();
builder.Services.AddOutcomeMvcFilter();   // registers [OutcomeResult] as global filter

var app = builder.Build();
app.MapControllers();
```

---

## Response Envelopes

### Error Envelope (`IErrorEnvelope`)

Zero-reflection, AOT-safe. One envelope for the whole app:

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
        Errors = details.Errors,
    };
}
```

Register: `options.Error.SetResponse<ErrorResponse>();`

Without `SetResponse`, the default is RFC 7807 `DefaultProblemDetails` with only `title`, `status`, `detail`.

### Success Envelope (`ISuccessEnvelope<T>`)

**Non-paginated (1-param generic):**

```csharp
public sealed class SuccessResponse<TData> : ISuccessEnvelope<TData>
{
    public int StatusCode { get; init; }
    public TData? Data { get; init; }

    public static ISuccessEnvelope<TData> From(
        TData data, IPaginationDetails? pagination, HttpContext httpContext, int statusCode)
        => new SuccessResponse<TData> { StatusCode = statusCode, Data = data };
}
```

**Paginated (2-param generic):**

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

Register:

```csharp
options.Success.SetResponse(typeof(SuccessResponse<>));    // non-paginated
options.Success.SetResponse(typeof(SuccessResponse<,>));   // paginated
```

When `options.Success.Enabled = false`, successes are returned raw with no wrapper.

### No-Payload Envelope (non-generic)

`http.Ok()` (no arguments) returns a status-only envelope with no `data` field. The type
needs only `From(HttpContext, int)` — no `ISuccessEnvelope<T>` required:

```csharp
public sealed class SuccessResponse
{
    public int StatusCode { get; init; }

    public static object From(HttpContext httpContext, int statusCode) =>
        new SuccessResponse { StatusCode = statusCode };
}
```

Register: `options.Success.SetResponse(typeof(SuccessResponse));`

Use with `.Produces<SuccessResponse>()` on the endpoint.

`SetResponse(Type)` auto-detects whether the type is generic or non-generic — no separate
method needed. A delegate overload is also available:
`options.Success.SetResponse((http, statusCode) => new SuccessResponse { StatusCode = statusCode });`

Without registration, the default is `DefaultSuccessResponse` — `{ "statusCode": 200 }`.

---

## Error Mapping

### `IHttpFailure` — Open/Closed Pattern

Failures that carry their own HTTP intent. No registration needed:

```csharp
public sealed class ProductNotFound : Failure, IHttpFailure
{
    public ProductNotFound(Guid productId)
        : base($"Product '{productId}' not found.", "product_not_found") { }

    public FailureDetails ToDetails() =>
        new(StatusCodes.Status404NotFound, Message, Code);
}
```

### `MapFailure<T>` — Startup Mappings

When you can't modify the failure type, register at startup. **Wins over** `IHttpFailure`:

```csharp
options.Error.MapFailure<QuotaExceededFailure>(f =>
    new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));
```

### `MapException<T>` — Exception Mappings

Specific exception types get their own HTTP intent. Mapped exceptions bypass `IncludeExceptionDetails`:

```csharp
options.Error.MapException<DomainException>(ex =>
    new FailureDetails(StatusCodes.Status422UnprocessableEntity, ex.Message, "domain_error"));
```

Subclasses inherit their nearest registered base's mapping.

### Resolution Order

**Failures:** `MapFailure<T>` > `IHttpFailure.ToDetails()` > 500 fallback
**Exceptions:** `MapException<T>` > unmapped (logged in full, generic 500, `IncludeExceptionDetails` gates message)

---

## Pagination

> **Recommendation:** define your own pagination types. The library ships with
> `Default*` reference implementations for convenience, but most apps want
> query shapes and metadata tuned to their domain (keyset, hybrid, opaque
> cursors, GraphQL-style connections, etc.). Implement the two interfaces
> below and `AddOutcome` will discover and configure them automatically.

### Prerequisites — the two interfaces

A custom pagination strategy is three types:

| Type | Role | Interface |
| --- | --- | --- |
| Query | Bound from the request, carries the caller's intent (page, cursor, after-key, …) | `IPaginatedQuery` |
| Result | Returned from the service, carries the data the service actually produced | _none — plain DTO_ |
| Info | Metadata describing the page; serialized as the second generic parameter of the success envelope | `IPaginationDetails` |

The two interfaces are tiny. `IPaginatedQuery` has one static-abstract member,
`Configure`; `IPaginationDetails` is an empty marker:

```csharp
public interface IPaginatedQuery
{
    static abstract void Configure(PaginationOptions options);
}

public interface IPaginationDetails { }
```

The interface does not enforce limits. Your query type owns its caps as **static
fields** (`MaxPage`, `MaxLimit`) plus a `Limit` property that clamps into them.
`Configure` copies the app's caps from `PaginationOptions` into those fields.
`AddOutcome` discovers every non-abstract `IPaginatedQuery` implementor across
loaded assemblies and calls `Configure` once at startup.

### Configuring Limits

```csharp
options.Pagination.MaxPage = 25;    // max page number for offset-style queries
options.Pagination.MaxLimit = 50;   // ceiling for every query type's Limit
```

Defaults are `int.MaxValue` (uncapped). Caps live on **static fields** of each
query type (not properties — ASP.NET Core's `RequestDelegateFactory` would bind
`public static` properties on `[AsParameters]` types as query parameters and emit
invalid IL). `AddOutcome` runs your `Configure` for you, so the fields pick up the
app's caps automatically.

### Custom Pagination (recommended)

Three small types, one endpoint. The query is bound by Minimal API binding,
the result comes from your service, and the info describes the page back to
the client:

```csharp
// Query — implements IPaginatedQuery
public sealed class KeysetQuery : IPaginatedQuery
{
    public static int MaxLimit = 50;
    private int _limit = 10;
    public string? AfterKey { get; set; }
    public int Limit { get => _limit; set => _limit = Math.Clamp(value, 1, MaxLimit); }
    public static void Configure(PaginationOptions options) =>
        MaxLimit = Math.Max(1, options.MaxLimit);
}

// Result — plain DTO returned by the service
public sealed class KeysetResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public string? NextKey { get; init; }
    public long? TotalCount { get; init; }
}

// Info — implements IPaginationDetails; serialized as the second generic
// parameter of the success envelope
public sealed class KeysetInfo : IPaginationDetails
{
    public string? NextKey { get; init; }
    public string? PreviousKey { get; init; }
    public bool HasMore { get; init; }
    public long? TotalItems { get; init; }
}

// Endpoint
group.MapGet("/keyset", (IProductService service, [AsParameters] KeysetQuery query, IHttpResultFactory http) =>
    Outcome.Success(service.GetProductsKeyset(query)).ToResult(http, page =>
        http.Ok(page.Items, new KeysetInfo
        {
            NextKey = page.NextKey,
            HasMore = page.NextKey is not null,
            TotalItems = page.TotalCount,
        })))
    .Produces<SuccessResponse<Product[], KeysetInfo>>();
```

If you have a convention for building `Info` from `Result` (e.g. a
`ToKeysetInfo()` helper on the result), use it. If not, build the info inline
— the contract is just `IPaginationDetails`, and the serializer doesn't care
which concrete type you used.

### Built-in `Default*` (reference implementations)

The package ships two ready-made strategies for users who don't want to write
their own:

| Strategy | Query | Result | Info |
| --- | --- | --- | --- |
| Offset | `DefaultOffsetQuery` | `DefaultOffsetResult<T>` | `DefaultOffsetInfo` |
| Cursor | `DefaultCursorQuery` | `DefaultCursorResult<T>` | `DefaultCursorInfo` |

They are **public** so they can be referenced and tested, but the library's
recommendation is to treat them as samples — copy them, rename them, drop
what you don't need. If `Default*` doesn't fit your domain, follow the
`Custom Pagination` section above.

**Offset:**

```csharp
// Service
public DefaultOffsetResult<Order> GetOrders(DefaultOffsetQuery query)
{
    var items = _orders.Skip(query.Offset).Take(query.Limit).ToArray();
    return new DefaultOffsetResult<Order> { Items = items, TotalCount = _orders.Count };
}

// Endpoint
group.MapGet("/", (IOrderService service, [AsParameters] DefaultOffsetQuery query, IHttpResultFactory http) =>
    Outcome.Success(service.GetOrders(query)).ToResult(http, page =>
        http.Ok(page.Items, page.ToOffsetInfo(query))))
    .Produces<SuccessResponse<Order[], DefaultOffsetInfo>>();
```

**Cursor:**

```csharp
// Service
public DefaultCursorResult<Order> GetOrdersCursor(DefaultCursorQuery cursorQuery)
{
    var startIndex = Decode(cursorQuery.Cursor); // null -> 0
    var items = _orders.Skip(startIndex).Take(cursorQuery.Limit).ToArray();
    var endIndex = startIndex + items.Length;

    return new DefaultCursorResult<Order>
    {
        Items = items,
        PreviousCursor = startIndex == 0 ? null : Encode(Math.Max(0, startIndex - cursorQuery.Limit)),
        NextCursor = endIndex < _orders.Count ? Encode(endIndex) : null,
        TotalCount = _orders.Count,
    };
}

// Endpoint
group.MapGet("/cursor", (IOrderService service, [AsParameters] DefaultCursorQuery cursorQuery, IHttpResultFactory http) =>
    Outcome.Success(service.GetOrdersCursor(cursorQuery)).ToResult(http, page =>
        http.Ok(page.Items, page.ToCursorInfo(cursorQuery))))
    .Produces<SuccessResponse<Order[], DefaultCursorInfo>>();
```

---

## JSON Serialization

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

// Minimal API
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// MVC (controllers) — configured separately
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
```

---

## See Also

- `outcome-core` — for creating `Outcome<T>`, `Failure`, `Unit`
- Full documentation: https://github.com/nebulaspace-id/outcome-csharp
