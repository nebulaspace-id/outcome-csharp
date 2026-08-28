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
- [Core types](#core-types)
- [Failures](#failures)
- [ASP.NET Core integration](#aspnet-core-integration)
- [MVC controllers](#mvc-controllers)
- [Response envelopes](#response-envelopes)
- [Exception handling](#exception-handling)
- [Pagination](#pagination)
- [JSON serialization](#json-serialization)
- [Testing](#testing)
- [Claude Code plugin](#claude-code-plugin)
- [Contributing](#contributing)
- [License](#license)

---

## Why Outcome?

Exceptions are for exceptional things. Most business errors ("not found", "duplicate",
"out of quota") are **expected outcomes**, not bugs. Modeling them as exceptions means
`try/catch` control flow, lost compiler help, and a muddled contract.

`Outcome<T>` makes failure part of the method signature. The caller is **forced** to handle
both branches — no hidden throw, no guesswork.

```csharp
// Before: failure is invisible
public async Task<Order> CreateOrder(CreateOrderRequest request)
{
    if (await _repo.Exists(request.Product))
        throw new ConflictException("Product already exists");
    return await _repo.Save(new Order(request.Product));
}

// After: the contract is explicit
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
dotnet add package NebulaSpace.Outcome.AspNetCore
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
    options.Success.SetResponse(typeof(SuccessResponse));      // no-payload (http.Ok())
    options.Error.SetResponse<ErrorResponse>();
});

// Then in your endpoint:
app.MapGroup("/greet")
   .MapGet("/{name}", (string name, IGreetingService service, IHttpResultFactory http) =>
       service.Greet(name).ToResult(http, greeting => http.Ok(new { greeting })));
```

Success invokes your handler; failure becomes the correct HTTP error automatically.
**No `try/catch` in the endpoint, ever.**

> The full runnable sample lives in [`samples/NebulaSpace.Outcome.Samples.WebApi`](samples/NebulaSpace.Outcome.Samples.WebApi).

---

## Packages

| Package | What's inside | Targets | Dependencies |
| --- | --- | --- | --- |
| `NebulaSpace.Outcome` | `Outcome`, `Outcome<T>`, `Failure`, `Unit` | **netstandard2.0** | none |
| `NebulaSpace.Outcome.AspNetCore` | `IHttpResultFactory`, options, failure types, pagination, exception handling | **net7.0** | core + `Microsoft.AspNetCore.App` |

The core stays `netstandard2.0` so it runs on .NET Framework 4.6.1+. The ASP.NET Core
package takes the `net7.0` floor to enable
[static abstract interface members](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/static-abstract-interface-members)
(C# 11). Both install on every later runtime.

> **Not AOT-safe.** The ASP.NET Core package uses `MakeGenericType` and `MethodInfo.Invoke`
> to close open-generic envelope types per request. This is by design — it gives you
> typed Swagger schemas (`Data: Order`) without hand-writing a factory per payload type.
> The trade-off is that NativeAOT is not supported. Use the delegate overloads
> (`SetResponse(Func<...>)`) if you need ahead-of-time compilation, at the cost of
> `Data: object` in Swagger.

---

## Core types

`Outcome<T>` is an immutable `readonly struct` — a discriminated union: either a **success**
(with a `Value`) or a **failure** (with a `Failure`). Its state is fixed at creation.

```csharp
Outcome<string> ok     = Outcome.Success("hello");      // success with a value
Outcome<Unit> done     = Outcome.Success();              // success, no payload
Outcome<string> failed = Outcome.Fail(new NotFoundFailure("Not found"));
Outcome<Order> order   = new Order(...);                 // implicit conversion
```

### Reading the state

| Member | Description |
| --- | --- |
| `IsSuccess` / `IsFailure` | Which branch you're in |
| `Value` | Payload on success (throws `InvalidOperationException` on failure) |
| `Failure` | The `Failure` on failure (throws on success) |
| `Match(onSuccess, onFailure)` | Collapse both branches into one value |
| `Deconstruct(...)` | C# `switch` / pattern matching support |

### Matching

```csharp
// Idiomatic — forces you to handle both branches
var display = result.Match(
    onSuccess: value   => $"OK: {value}",
    onFailure: failure => $"Error: {failure.Message}");

// Pattern matching
string message = result switch
{
    (true, var value, _)    => $"Value is {value}",
    (false, _, var failure) => $"Failed with {failure.Message}",
    _                       => "Unreachable",
};
```

### Service patterns

```csharp
public async Task<Outcome<Order>> GetOrder(int id)
{
    var order = await _repo.FindById(id);
    if (order is null)
        return Outcome.Fail(new NotFoundFailure("Order not found"));
    return order;   // implicit conversion
}

public async Task<Outcome<Unit>> DeleteOrder(int id)
{
    if (!await _repo.Exists(id))
        return Outcome.Fail(new NotFoundFailure("Order not found"));
    await _repo.Remove(id);
    return Outcome.Success();
}
```

---

## Failures

Failures are immutable values derived from `Failure`. The base type carries a
`Message` and an optional `Code`:

```csharp
public sealed class NotFoundFailure : Failure
{
    public NotFoundFailure(string message) : base(message, "not_found") { }
}
```

### Built-in failures

These ship in the **AspNetCore** package and know their own HTTP intent. No registration needed:

| Failure | HTTP | Code |
| --- | --- | --- |
| `BadRequestFailure` | 400 | `bad_request` |
| `UnauthorizedFailure` | 401 | `unauthorized` |
| `ForbiddenFailure` | 403 | `forbidden` |
| `NotFoundFailure` | 404 | `not_found` |
| `ConflictFailure` | 409 | `conflict` |
| `ValidationFailure` | 400 | `validation` |
| `InternalServerErrorFailure` | 500 | `internal_server_error` |

### Custom failures — three approaches

**1. `MapFailure<T>` at startup** — when you can't modify the failure type:

```csharp
options.Error.MapFailure<QuotaExceededFailure>(f =>
    new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));
```

**2. Implement `IHttpFailure`** — the failure carries its own HTTP intent (Open/Closed):

```csharp
public sealed class QuotaExceededFailure : Failure, IHttpFailure
{
    public QuotaExceededFailure(string message) : base(message, "quota_exceeded") { }

    public FailureDetails ToDetails() =>
        new(StatusCodes.Status422UnprocessableEntity, Message, Code);
}
```

**3. Typed error envelope** — changes the shape of *every* error response. See [Response envelopes](#response-envelopes).

### Resolution order

1. `MapFailure<T>` wins, even over built-in failures.
2. `IHttpFailure` maps itself.
3. Unknown failures degrade to 500 (never throws).

A subclass inherits its nearest ancestor's mapping.

---

## ASP.NET Core integration

### Register

```csharp
builder.Services.AddOutcome(options =>
{
    options.Success.SetResponse(typeof(SuccessResponse<>));    // non-paginated
    options.Success.SetResponse(typeof(SuccessResponse<,>));   // paginated
    options.Success.SetResponse(typeof(SuccessResponse));      // no-payload (http.Ok())
    options.Error.SetResponse<ErrorResponse>();

    options.Error.MapFailure<QuotaExceededFailure>(f =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));
});
```

`AddOutcome` registers `OutcomeOptions` and `IHttpResultFactory` as singletons.
**Multiple apps in one process can configure the library independently.**

### From outcome to response

Inject `IHttpResultFactory` into your endpoint or service:

```csharp
// Sync
result.ToResult(http, order => http.Created(order, o => o.Id));

// Async
await outcomeTask.ToResultAsync(http, order => http.Created(order, o => o.Id));

// Async success handler
await outcomeTask.ToResultAsync(http, async order => await store.Save(order, http));

// Failure-only
failure.ToErrorResponse(http);
```

### `IHttpResultFactory` helpers

| Helper | Behavior |
| --- | --- |
| `http.Ok()` | 200 with no payload (status-only envelope) |
| `http.Ok(data)` | 200, raw or wrapped per `Success.Enabled` |
| `http.Ok(data, pagination)` | 200 with pagination metadata |
| `http.Created(data, idSelector)` | 201 with `Location` derived from the request path |
| `http.Created(data, locationUri)` | 201 with a manually specified `Location` |
| `http.NoContent()` | 204 |
| `http.BadRequest(...)` / `http.NotFound(...)` / `http.Conflict(...)` | 4xx with the configured error envelope |
| `http.FromFailure(failure)` | Map any failure to its response |

`http.Created` builds the `Location` header from the request path automatically:

```csharp
return result.ToResult(http, order => http.Created(order, o => o.Id));
// POST /orders       -> Location: /orders/{id}
// POST /admin/orders -> Location: /admin/orders/{id}
```

For non-standard URIs, use the manual overload:

```csharp
return result.ToResult(http, order => http.Created(order, $"/orders/{order.Id}/items/42"));
```

---

## MVC controllers

The library supports MVC controllers through two patterns — both reuse the same
`IHttpResultFactory`, envelopes, and failure mapping as Minimal APIs.

### Pattern 1: `[OutcomeResult]` filter (zero ceremony)

Return `Outcome<T>` directly from an action. The filter converts it to the correct HTTP
response automatically:

```csharp
[ApiController, Route("products")]
[OutcomeResult]                            // per-controller or global
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service) => _service = service;

    [HttpGet("{id:guid}")]
    public Outcome<Product> Get(Guid id) => _service.GetProduct(id);   // 200 or 404

    [HttpDelete("{id:guid}")]
    public Outcome<Unit> Delete(Guid id) => _service.DeleteProduct(id); // 204 or 404
}
```

### Pattern 2: explicit `ToActionResult` / `ToActionResultAsync`

When you need a custom success response (201 with `Location`, pagination metadata, etc.):

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

### Register

```csharp
builder.Services.AddControllers();
builder.Services.AddOutcomeMvcFilter();   // registers [OutcomeResult] as a global filter

var app = builder.Build();
app.MapControllers();
```

> The full controller sample lives alongside the Minimal API sample in
> [`samples/NebulaSpace.Outcome.Samples.WebApi/Features/Products/ProductsController.cs`](samples/NebulaSpace.Outcome.Samples.WebApi/Features/Products/ProductsController.cs).

---

## Response envelopes

> **You are strongly encouraged to define your own envelopes.**

The defaults are minimal: RFC 7807 `DefaultProblemDetails` for errors and a thin
`{ statusCode, data }` wrapper for successes. Real APIs usually want a consistent,
documented shape — `traceId`, `code`, field-level errors. Typed envelopes give you:

- **Your type, serialized as-is.** The body *is* `ErrorResponse` / `SuccessResponse<T>`.
  OpenAPI documents the exact shape. Properties are real C#.
- **One consistent body everywhere.** Failures, `MapFailure` results, mapped exceptions,
  and unhandled exceptions all render through the same envelope.
- **Fail-fast at startup.** If a type doesn't implement the interface, `SetResponse`
  throws immediately.

### Success envelope

Implement `ISuccessEnvelope<T>` on an open-generic type. The library closes `T` per
request so Swagger sees the exact payload type:

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

For **paginated** responses, use a 2-param generic so the second parameter carries the
concrete pagination type (`DefaultOffsetInfo`, `DefaultCursorInfo`, or your own):

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
options.Success.SetResponse(typeof(SuccessResponse<>));   // non-paginated
options.Success.SetResponse(typeof(SuccessResponse<,>));  // paginated
```

```csharp
.Produces<SuccessResponse<Order>>()                       // Swagger knows Data: Order
.Produces<SuccessResponse<Order[], DefaultOffsetInfo>>()   // Swagger knows Data + Pagination
```

When `Success.Enabled` is `false`, successes are returned raw with no wrapper.

### No-payload envelope

`http.Ok()` (no arguments) returns a status-only envelope with no `data` field. The
non-generic envelope type needs only a `From(HttpContext, int)` method — no
`ISuccessEnvelope<T>` contract required:

```csharp
public sealed class SuccessResponse
{
    public int StatusCode { get; init; }

    public static object From(HttpContext httpContext, int statusCode) =>
        new SuccessResponse { StatusCode = statusCode };
}
```

```csharp
options.Success.SetResponse(typeof(SuccessResponse));   // non-generic, no payload
```

```csharp
group.MapGet("/ping", (IHttpResultFactory http) => http.Ok())
    .Produces<SuccessResponse>();                       // Swagger: { statusCode: 200 }
```

Without registration, the default is `DefaultSuccessResponse` — `{ "statusCode": 200 }`.

### Error envelope

`ErrorOptions.SetResponse<T>()` makes the error body any class implementing
`IErrorEnvelope`. Zero-reflection dispatch via `static abstract`:

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

```csharp
options.Error.SetResponse<ErrorResponse>();
```

```csharp
.Produces<SuccessResponse<Order>>()
.Produces<ErrorResponse>(StatusCodes.Status404NotFound);
```

```json
{
  "statusCode": 404,
  "code": "not_found",
  "message": "Order not found",
  "traceId": "0HLMS12B6V4H6:00000001"
}
```

Without `SetResponse`, the default is RFC 7807 with only `title`, `status`, `detail`.

---

## Exception handling

Unhandled exceptions normally fall through to ASP.NET Core's default error page — a
different shape. The exception-handling middleware converts them into the **same envelope**
as expected failures:

```csharp
builder.Services.AddOutcome(options =>
{
    options.Error.MapException<DomainException>(ex =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, ex.Message, ex.Code));

    // FluentValidation → 400 with field errors (RFC 9457)
    options.Error.MapException<ValidationException>(ex =>
        new FailureDetails(
            StatusCodes.Status400BadRequest,
            "Validation Error",
            code: "validation_error",
            errors: ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));

    options.Error.IncludeExceptionDetails = builder.Environment.IsDevelopment();
});

var app = builder.Build();
app.UseOutcomeExceptionHandler();   // BEFORE routing
```

### How mapping works

- **`MapException<T>`** works like `MapFailure<T>` — register any exception type, the
  mapper returns HTTP intent, the error envelope renders it. Mapped exceptions bypass
  `IncludeExceptionDetails`.
- **Unmapped exceptions** are logged in full (with stack trace) and become a generic 500.
  `IncludeExceptionDetails = false` (default) never leaks `ex.Message` to the client.
  If the response has already started, the exception is re-thrown.

### Ordering with custom middleware

`UseOutcomeExceptionHandler` only renders the response. To log exceptions to your own
store, register your middleware **before** the handler — it catches, records, re-throws:

```csharp
// 1. Your logging middleware FIRST (outermost)
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

// 2. THEN the outcome exception handler (inner)
app.UseOutcomeExceptionHandler();
```

Middleware runs outer-to-inner: your middleware listed first wraps everything after it.

---

## Pagination

Offset, cursor, and custom strategies all work through one `IPaginationDetails` marker
interface and `http.Ok(data, pagination)`.

> **You are encouraged to build your own pagination types.** The library ships
> `DefaultOffsetQuery` / `DefaultCursorQuery` as reference implementations, but real APIs
> benefit from owning the query, result, and metadata types — app-specific caps, naming,
> extra fields, or a different strategy altogether. The sample project demonstrates this
> with custom `OffsetQuery` and `CursorQuery` types built from scratch.
> See [`samples/NebulaSpace.Outcome.Samples.WebApi/Shared/`](samples/NebulaSpace.Outcome.Samples.WebApi/Shared/).

### Configuring limits

```csharp
options.Pagination.MaxPage = 25;    // max page number (offset)
options.Pagination.MaxLimit = 50;   // max items per page (both strategies)
```

Defaults are `int.MaxValue` (uncapped). Limits live on **static fields** (not properties)
to avoid ASP.NET Core's `RequestDelegateFactory` binding them as query parameters.

### Building your own pagination

Every pagination strategy needs three types:

| Type | Implements | Role |
| --- | --- | --- |
| **Query** | `IPaginatedQuery` | Incoming parameters, bound from query string via `[AsParameters]` |
| **Result** | — | Service-layer return: items + raw navigation data |
| **Info** | `IPaginationDetails` | Response metadata that goes into the success envelope |

`AddOutcome` discovers all `IPaginatedQuery` implementors at startup and calls
`Configure(PaginationOptions)` on each — so your custom query types pick up the app's
caps automatically, with zero wiring.

```csharp
// 1. Query — AddOutcome calls Configure() automatically
public sealed class OffsetQuery : IPaginatedQuery
{
    public static int MaxPage = int.MaxValue;
    public static int MaxLimit = int.MaxValue;

    private int _page = 1;
    private int _limit = 10;

    public int Page  { get => _page;  set => _page  = Math.Clamp(value, 1, MaxPage); }
    public int Limit { get => _limit; set => _limit = Math.Clamp(value, 1, MaxLimit); }
    public int Offset => (Page - 1) * Limit;

    public static void Configure(PaginationOptions options)
    {
        MaxPage  = Math.Max(1, options.MaxPage);
        MaxLimit = Math.Max(1, options.MaxLimit);
    }
}

// 2. Result — returned by the service layer
public sealed class OffsetResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public long TotalCount { get; set; }

    public OffsetInfo ToOffsetInfo(OffsetQuery query) =>
        OffsetInfo.Create(query, (int)TotalCount);
}

// 3. Info — goes into the success envelope
public sealed class OffsetInfo : IPaginationDetails
{
    public int Page { get; set; }
    public int Limit { get; set; }
    public int TotalPages { get; set; }
    public long? TotalItems { get; set; }
    public bool HasMore => Page < TotalPages;

    public static OffsetInfo Create(OffsetQuery query, int totalItems) => new()
    {
        Page = query.Page,
        Limit = query.Limit,
        TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / query.Limit),
        TotalItems = totalItems,
    };
}
```

### Using pagination in an endpoint

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

### Built-in defaults

The library ships ready-made types if you don't need custom naming or extra fields:

| Type | Description |
| --- | --- |
| `DefaultOffsetQuery` | `page` / `limit` with `MaxPage` / `MaxLimit` caps |
| `DefaultOffsetResult<T>` | `Items` + `TotalCount` |
| `DefaultOffsetInfo` | `Page`, `Limit`, `TotalPages`, `TotalItems`, `HasMore` |
| `DefaultCursorQuery` | `cursor` / `limit` with `MaxLimit` cap |
| `DefaultCursorResult<T>` | `Items`, `PreviousCursor`, `NextCursor`, `TotalCount` |
| `DefaultCursorInfo` | `PreviousCursor`, `NextCursor`, `HasMore`, `TotalItems` |

```csharp
// Using the built-in defaults directly
group.MapGet("/", (IOrderService service, [AsParameters] DefaultOffsetQuery query, IHttpResultFactory http) =>
    Outcome.Success(service.GetOrders(query)).ToResult(http, page =>
        http.Ok(page.Items, page.ToOffsetInfo(query))))
    .Produces<SuccessResponse<Order[], DefaultOffsetInfo>>();
```

### Custom strategies

Any strategy (keyset, time-based, composite) works — implement the same three-type
pattern (query, result, info) and it plugs straight into `http.Ok(data, pagination)`.
The [sample project](samples/NebulaSpace.Outcome.Samples.WebApi/Shared/) demonstrates this
with custom offset and cursor types built from scratch.

---

## JSON serialization

The library writes envelopes with `WriteAsJsonAsync`, which honors your app's JSON
serializer options. A recommended setup (nulls omitted, camelCase, enums as strings):

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

## Testing

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
Claude accurate guidance for using the library.

### Install

```
/plugin marketplace add nebulaspace-id/outcome-csharp
/plugin install outcome-tools@outcome-csharp
```

### What's inside

| Skill | Covers |
| --- | --- |
| `outcome-core` | `Outcome<T>`, `Failure`, `Unit`, matching, service patterns |
| `outcome-aspnetcore` | `AddOutcome`, `IHttpResultFactory`, envelopes, error mapping, pagination, endpoints |

Claude automatically loads the relevant skill when you write code that uses the library.

### Team auto-config

Pre-register the marketplace for your team so every contributor only needs the install
step. Add to `.claude/settings.json`:

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
