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

> **AOT warning:** `SetResponse(typeof(...))` uses `MakeGenericType` and is not AOT-safe. For NativeAOT, use the delegate overload `SetResponse(Func<...>)`.

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

### Pattern 1: `[OutcomeResult]` filter (zero ceremony)

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

### Configuring Limits

```csharp
options.Pagination.MaxPage = 25;    // max page number for offset
options.Pagination.MaxLimit = 50;   // max items per page for both strategies
```

Defaults are `int.MaxValue` (uncapped). Limits are on **static fields** (not properties — RDE IL bug).

### Offset Pagination

```csharp
// Service
public OffsetResult<Order> GetOrders(OffsetQuery query)
{
    var items = _orders.Skip(query.Offset).Take(query.Limit).ToArray();
    return new OffsetResult<Order> { Items = items, TotalCount = _orders.Count };
}

// Endpoint
group.MapGet("/", (IOrderService service, [AsParameters] OffsetQuery query, IHttpResultFactory http) =>
    Outcome.Success(service.GetOrders(query)).ToResult(http, page =>
        http.Ok(page.Items, page.ToOffsetInfo(query))))
    .Produces<SuccessResponse<Order[], OffsetInfo>>();
```

### Cursor Pagination

```csharp
// Service
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

// Endpoint
group.MapGet("/cursor", (IOrderService service, [AsParameters] CursorQuery cursorQuery, IHttpResultFactory http) =>
    Outcome.Success(service.GetOrdersCursor(cursorQuery)).ToResult(http, page =>
        http.Ok(page.Items, page.ToCursorInfo(cursorQuery))))
    .Produces<SuccessResponse<Order[], CursorInfo>>();
```

### Custom Pagination

Three types needed: query (implements `IPaginatedQuery`), result, and metadata (implements `IPaginationDetails`). `AddOutcome` discovers and configures query types automatically.

```csharp
// Query
public sealed class KeysetQuery : IPaginatedQuery
{
    public static int MaxLimit = 50;
    private int _limit = 10;
    public string? AfterKey { get; set; }
    public int Limit { get => _limit; set => _limit = Math.Clamp(value, 1, MaxLimit); }
    public static void Configure(PaginationOptions options) => MaxLimit = Math.Max(1, options.MaxLimit);
}

// Metadata
public sealed class KeysetInfo : IPaginationDetails
{
    public string? NextKey { get; set; }
    public string? PreviousKey { get; set; }
    public bool HasMore { get; set; }
    public long? TotalItems { get; set; }
}

// Endpoint
group.MapGet("/keyset", (IProductService service, [AsParameters] KeysetQuery query, IHttpResultFactory http) =>
    Outcome.Success(service.GetProductsKeyset(query)).ToResult(http, page =>
        http.Ok(page.Items, page.ToKeysetInfo())))
    .Produces<SuccessResponse<Product[], KeysetInfo>>();
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
