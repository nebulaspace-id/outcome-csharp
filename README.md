<div align="center">

# NebulaSpace Outcome

**A dependency-free result pattern for C# and .NET. `Outcome<T>` carries success or failure as a value instead of an exception, and the ASP.NET Core package maps it to the HTTP response shape you define, serialized by your own `System.Text.Json` options.**

[![NuGet][outcome-shield]][outcome-nuget]
[![NuGet][aspnetcore-shield]][aspnetcore-nuget]
[![GitHub][github-shield]][github]
[![MIT License][license-shield]][license]

Result pattern · `Outcome<T>` · `Failure` · Minimal API & MVC · Custom envelopes · `System.Text.Json` friendly · Exception to error mapping

</div>

Outcome is a result pattern (also called an outcome or railway-oriented result type) for
.NET services. Expected failures, such as not found, conflict, or validation errors,
become values your code returns, and the ASP.NET Core integration turns those values into
HTTP responses with your own success and error envelopes. It works with .NET Minimal API
endpoints, MVC controllers, and any `JsonSerializerOptions` you already use.

---

## Install

```bash
dotnet add package NebulaSpace.Outcome
dotnet add package NebulaSpace.Outcome.AspNetCore
```

## Contents

- [The idea in 30 seconds](#the-idea-in-30-seconds)
- [What you write vs what you customize](#what-you-write-vs-what-you-customize)
  - [Required: two types, one registration](#required-two-types-one-registration)
  - [No payload: `Unit`](#no-payload-unit)
  - [Optional: your response API, your rules](#optional-your-response-api-your-rules)
- [Wire format](#wire-format)
  - [Serialized with your JSON options](#serialized-with-your-json-options)
  - [Metadata and pagination](#metadata-and-pagination)
  - [One strategy or both](#one-strategy-or-both)
- [Keep a legacy response shape](#keep-a-legacy-response-shape)
- [API documentation](#api-documentation)
- [Failures](#failures)
  - [Returning a failure from a service](#returning-a-failure-from-a-service)
- [IHttpResultFactory](#ihttpresultfactory)
  - [Outcome extensions](#outcome-extensions)
  - [Manual Match (alternative)](#manual-match-alternative)
- [MVC controllers](#mvc-controllers)
  - [Pattern 1: `[OutcomeResult]` filter](#pattern-1-outcomeresult-filter)
  - [Pattern 2: explicit `ToActionResult`](#pattern-2-explicit-toactionresult)
- [Exception handling](#exception-handling)
- [Rate limiting](#rate-limiting)

---

## The idea in 30 seconds

A service method returns `Outcome<T>`: either a **success with a value** or a **failure
with a reason**. No exceptions for expected errors, no `null` checks, no hand-rolled
result classes. The endpoint turns the outcome into an HTTP response in one line:

```csharp
// Service. Same shape whether the call can fail or not.
public Outcome<Product> GetProduct(Guid id) =>
    _repo.GetById(id) ?? new ProductNotFound(id);

public Outcome<OffsetResult<Product>> GetProducts(OffsetQuery q) =>
    _repo.GetAll(q);                       // a page of items is just a value

// Endpoint. The failure branch is mapped for free.
group.MapGet("/{id:guid}", (Guid id, IProductService service, IHttpResultFactory http) =>
{
    var result = service.GetProduct(id);
    return result.ToResult(http, product => http.Ok(product, new Meta()));
});
```

Implicit conversions keep call sites short: `return product` lifts a raw value into a
success, `return new ProductNotFound(id)` lifts a failure. The same `Outcome<T>` works
for single reads, pages, commands, and deletes.

| Member | Description |
| --- | --- |
| `IsSuccess` / `IsFailure` | Which branch |
| `Value` / `Failure` | Access payload or error |
| `Match(onSuccess, onFailure)` | Collapse both branches into one value |

---

## What you write vs what you customize

This library is bring-your-own-response-types: the success and error bodies are always
yours, not the library's. It owns status mapping and the wiring; you own the JSON your
clients see.

### Required: two types, one registration

Define the body of a success and of an error. The package ships `Default*` envelopes so
tests and demos work out of the box; production APIs should define their own.

```csharp
// 1. Success body. One open generic covers every endpoint.
public sealed class SuccessResponse<TData> : ISuccessEnvelope<TData>
{
    public int StatusCode { get; init; }
    public TData? Data { get; init; }
    public Meta? Meta { get; init; }

    public static ISuccessEnvelope<TData> From(
        TData data, object? meta, HttpContext httpContext, int statusCode)
        => new SuccessResponse<TData>
        {
            StatusCode = statusCode,
            Data = data,
            // Meta arrives as object. The library never knows your type, so narrow it here.
            Meta = meta as Meta ?? new Meta(),
        };
}

// 2. Error body.
public sealed class ErrorResponse : IErrorEnvelope
{
    public int? StatusCode { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }
    public string? TraceId { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static IErrorEnvelope From(FailureDetails details, HttpContext context) => new ErrorResponse
    {
        StatusCode = details.StatusCode, Code = details.Code,
        Message = details.Message, TraceId = context.TraceIdentifier, Errors = details.Errors,
    };
}

// 3. Register at startup.
builder.Services.AddOutcome(options =>
{
    options.Success.SetResponse(typeof(SuccessResponse<>));   // closes T per request
    options.Error.SetResponse<ErrorResponse>();
});
```

That is the entire required surface. `http.Ok()` with no payload reuses the success
envelope: `Data` stays null and `JsonIgnoreCondition.WhenWritingNull` omits the key.

### No payload: `Unit`

When an operation succeeds with nothing to return (delete, ping, an acknowledge-only
command), use `Unit`. It is a tiny struct from the core package that means "no value",
so the outcome type still reads honestly:

```csharp
// Service. The success branch carries no domain value.
public Outcome<Unit> DeleteProduct(Guid id)
{
    if (!_repo.Delete(id))
        return new ProductNotFound(id);

    return Outcome.Success();       // Outcome<Unit>
}

// Endpoint. 204 is the usual choice for a successful delete:
group.MapDelete("/{id:guid}", (Guid id, IProductService service, IHttpResultFactory http) =>
{
    var result = service.DeleteProduct(id);
    return result.ToResult(http, _ => http.NoContent());
})
    .Produces(StatusCodes.Status204NoContent)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound);
```

`Outcome<Unit>` still flows through the same envelope when you want a 200 body instead
of 204: `http.Ok()` returns the registered envelope with a null `Data`, which
`WhenWritingNull` omits. In Swagger, document that shape as
`.Produces<SuccessResponse<Unit>>()` (or
`[ProducesResponseType<SuccessResponse<Unit>>(StatusCodes.Status200OK)]` in MVC), like
the sample's ping endpoint.

### Optional: your response API, your rules

| Want | Write | Skip it and |
| --- | --- | --- |
| Metadata (`meta` timestamp, pagination, request id) | A `Meta` type plus a `Pagination` type | Responses are just `statusCode` and `data` |
| Pagination (offset, cursor, or both) | Query and result types per strategy you use | The library ships none: no caps, no opinion |
| Links / HATEOAS | A `Links` (or similar) property on your own `Meta` | The library has no HATEOAS support today: no link relations, no rel builders, no automatic `self`/`next` links. Responses carry `data` and `meta` only, so hypermedia is up to your envelope |
| Custom error status per failure | `IHttpFailure` on the failure, or `options.Error.MapFailure<T>(…)` | Unknown failures fall back to 500, never throw |
| Exceptions mapped to your envelope | `options.Error.MapException<T>(…)` plus `app.UseOutcomeExceptionHandler()` | Unhandled exceptions become a plain 500 |
| Casing and null handling | Your usual `HttpJsonOptions` | Host defaults apply |

None of it is required. A minimal API can run on the two envelopes alone.

---

## Wire format

What the sample API emits:

```jsonc
// GET /products/42
{ "statusCode": 200, "data": { "id": "…", "name": "…" }, "meta": { "timestamp": "…" } }

// GET /products?page=1&limit=10
{
  "statusCode": 200,
  "data": [ … ],
  "meta": {
    "pagination": { "page": 1, "limit": 10, "totalItems": 100, "totalPages": 10, "hasMore": true },
    "timestamp": "…"
  }
}

// GET /products/unknown-id
{ "statusCode": 404, "code": "product_not_found", "message": "Product '…' not found.", "traceId": "…" }
```

None of this is fixed. Want `{ code, message, data }` instead? See the legacy section
below: you keep the shape, the library does the rest.

### Serialized with your JSON options

The envelopes are plain objects with no serialization attributes, so
`System.Text.Json` writes them with the `JsonSerializerOptions` your app already
configures. Nothing in the library overrides your naming policy, ignore condition, or
converters:

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
```

Every behavior shown in this README follows from options like these, not from the
library: `WhenWritingNull` drops a null `Data` (the ping response), drops `meta` when an
endpoint attached none, and drops the `pagination` object on unpaged responses. Camel
casing and your value converters apply to `data`, `meta`, and the error body alike.
MVC responses honor the same options through `Microsoft.AspNetCore.Mvc.JsonOptions`.

### Metadata and pagination

`Meta` is built in the endpoint layer and handed to `http.Ok(data, meta)`. Pagination
sits nested under `meta.pagination`. When the response is not paged, `Pagination` stays
null and the whole key disappears from the JSON.

```csharp
public sealed class Meta
{
    public Pagination? Pagination { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    // One overload per strategy you support. Meta only nests; the math lives in Pagination.
    public Meta SetPagination<T>(OffsetResult<T> result, OffsetQuery query) => new()
    {
        Pagination = Pagination.Offset(result, query),
    };

    public Meta SetPagination<T>(CursorResult<T> result) => new()
    {
        Pagination = Pagination.Cursor(result),
    };
}

// One Pagination type covers both strategies. Every field is nullable, so
// WhenWritingNull drops the ones the strategy in play does not fill.
public sealed class Pagination
{
    public int? Page { get; init; }
    public int? Limit { get; init; }
    public long? TotalItems { get; init; }
    public int? TotalPages { get; init; }
    public bool? HasMore { get; init; }
    public string? PreviousCursor { get; init; }
    public string? NextCursor { get; init; }

    // Offset: echoes the request's page/limit, derives the rest from TotalCount.
    public static Pagination Offset<T>(OffsetResult<T> result, OffsetQuery query)
    {
        var totalPages = result.TotalCount == 0
            ? 0
            : (int)Math.Ceiling((double)result.TotalCount / query.Limit);

        return new Pagination
        {
            Page = query.Page,
            Limit = query.Limit,
            TotalItems = result.TotalCount,
            TotalPages = totalPages,
            HasMore = query.Page < totalPages,
        };
    }

    // Cursor: self-describing. Nothing is echoed from the request.
    public static Pagination Cursor<T>(CursorResult<T> result) => new()
    {
        PreviousCursor = result.PreviousCursor,
        NextCursor = result.NextCursor,
        HasMore = result.HasMore,
        TotalItems = result.TotalCount,
    };
}
```

### One strategy or both

Pick the strategy your API actually uses. Supporting only offset? Write `OffsetQuery`
and `OffsetResult<T>`, use the offset overload of `SetPagination`, and never touch
cursor types. Only cursor? Same thing in reverse. Both work too: the strategies share
one `Pagination` type, so an offset response emits `page`/`limit`/`totalPages` and a
cursor response emits `previousCursor`/`nextCursor`, with no extra types and no unused
fields in either JSON.

One thing the library never does for you: cap the request. `MaxPage` and `MaxLimit`
live on your query type (see the sample's `Shared/OffsetQuery` and `Shared/CursorQuery`),
clamped in the setters. A client sending `?page=999999` gets your cap, because the
bound value never leaves the query object unclamped.

```csharp
// Offset only: query the request binds to, page the service returns.
// Caps are your job, not the library's: clamp every value a client can send.
public sealed class OffsetQuery : IPaginatedQuery
{
    private const int MaxPage = 25;
    private const int MaxLimit = 50;

    private int _page = 1;
    private int _limit = 10;

    public int Page  { get => _page;  set => _page  = Math.Clamp(value, 1, MaxPage); }
    public int Limit { get => _limit; set => _limit = Math.Clamp(value, 1, MaxLimit); }
    public int Offset => (Page - 1) * Limit;
}

public sealed class OffsetResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public long TotalCount { get; init; }
}

// A page is just a value in the outcome, so failures work like any other call.
public Outcome<OffsetResult<Product>> GetProducts(OffsetQuery query) =>
    _repo.GetAll(query);

group.MapGet("/", (IProductService service, [AsParameters] OffsetQuery query, IHttpResultFactory http) =>
{
    var result = service.GetProducts(query);
    return result.ToResult(http, page =>
    {
        var meta = new Meta().SetPagination(page, query);
        return http.Ok(page.Items, meta);
    });
})
    .Produces<SuccessResponse<Product[]>>();
```

Cursor strategy, same shape:

```csharp
public sealed class CursorQuery : IPaginatedQuery
{
    private const int MaxLimit = 50;

    private int _limit = 10;

    public string? Cursor { get; set; }
    public int Limit { get => _limit; set => _limit = Math.Clamp(value, 1, MaxLimit); }
}

public sealed class CursorResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public string? PreviousCursor { get; init; }
    public string? NextCursor { get; init; }
    public bool HasMore => NextCursor is not null;
    public long? TotalCount { get; init; }
}

// The overload picks the strategy from the argument types.
group.MapGet("/cursor", (IProductService service, [AsParameters] CursorQuery query, IHttpResultFactory http) =>
{
    var result = service.GetProductsCursor(query);
    return result.ToResult(http, page =>
    {
        var meta = new Meta().SetPagination(page);
        return http.Ok(page.Items, meta);
    });
})
    .Produces<SuccessResponse<Product[]>>();
```

---

## Keep a legacy response shape

Many teams have clients (mobile apps, frontend code, other services) already depending
on an old envelope like `{ code, message, data }`. This library does not force a new
shape. You keep the old wire format; the code around it gets clean.

A typical legacy controller writes the envelope by hand, in every action:

```csharp
// BEFORE: the envelope is copy-pasted per endpoint and already drifts between actions.
[HttpGet("{id:guid}")]
public async Task<IActionResult> Get(Guid id)
{
    try
    {
        var product = await _repo.GetByIdAsync(id);
        if (product is null)
            return NotFound(new { code = 404, message = "Product not found" });

        return Ok(new { code = 200, message = "success", data = product });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Get failed");
        return StatusCode(500, new { code = 500, message = "Internal server error" });
    }
}

[HttpGet]
public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int limit = 10)
{
    try
    {
        var (items, total) = await _repo.GetProductsAsync((page - 1) * limit, limit);
        return Ok(new
        {
            code = 200,
            message = "success",
            data = items,
            meta = new { page, limit, total }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "List failed");
        return StatusCode(500, new { code = 500, message = "Internal server error" });
    }
}
```

The problems: the success shape is re-declared in every action (and the two above
already differ), try/catch repeats, and every failure collapses into the same 500.

Define that legacy shape once, in your own envelope types. The JSON your clients
receive stays the same:

```csharp
// AFTER, part 1: the old wire format, defined once.
public sealed class LegacySuccess<TData> : ISuccessEnvelope<TData>
{
    public int Code { get; init; }
    public string Message { get; init; } = "success";
    public TData? Data { get; init; }

    public static ISuccessEnvelope<TData> From(
        TData data, object? meta, HttpContext httpContext, int statusCode)
        => new LegacySuccess<TData> { Code = statusCode, Data = data };
}

public sealed class LegacyError : IErrorEnvelope
{
    public int Code { get; init; }
    public string Message { get; init; } = "";

    public static IErrorEnvelope From(FailureDetails details, HttpContext context) => new LegacyError
    {
        Code = details.StatusCode ?? 500,
        Message = details.Message ?? "Internal server error",
    };
}

// Register them instead of the modern envelopes.
builder.Services.AddOutcome(options =>
{
    options.Success.SetResponse(typeof(LegacySuccess<>));
    options.Error.SetResponse<LegacyError>();
});
```

The controllers shrink to the outcome flow. Same JSON, less code:

```csharp
// AFTER, part 2: same response bodies, no hand-built envelopes.
public Outcome<Product> GetProduct(Guid id) =>
    _repo.GetById(id) ?? new ProductNotFound("Product not found");

group.MapGet("/{id:guid}", (Guid id, IProductService service, IHttpResultFactory http) =>
{
    var result = service.GetProduct(id);
    return result.ToResult(http, product => http.Ok(product));
});

group.MapGet("/", (IProductService service, [AsParameters] OffsetQuery query, IHttpResultFactory http) =>
{
    var result = service.GetProducts(query);
    return result.ToResult(http, page => http.Ok(page.Items));
});
```

```jsonc
// The client sees the same body it always did:
{ "code": 200, "message": "success", "data": { "id": "…" } }
{ "code": 404, "message": "Product not found" }
```

Migrating later is one file swap: change the envelope types, register the new ones,
keep the controllers.

---

## API documentation

The success envelope is an open generic, so Swagger documents the exact payload of each
endpoint instead of an opaque blob. Declare the shapes with `.Produces<T>()` on the
endpoint:

```csharp
group.MapGet("/", (IProductService service, [AsParameters] OffsetQuery query, IHttpResultFactory http) =>
{
    var result = service.GetProducts(query);
    return result.ToResult(http, page =>
    {
        var meta = new Meta().SetPagination(page, query);
        return http.Ok(page.Items, meta);
    });
})
    .Produces<SuccessResponse<Product[]>>()                                     // 200 shape
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)                   // 400 shape
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound);                    // 404 shape
```

`SuccessResponse<Product[]>` closes `TData` per endpoint, so the OpenAPI schema shows
`data: Product[]` and your `Meta` fields, not `data: object`. The failure declarations
give the error envelope its own entry per status code.

The sample wires Swagger in `Program.cs`: `AddEndpointsApiExplorer()`,
`AddSwaggerGen(...)`, and `MapOpenApi()` (or `UseSwagger()` for the classic UI). For MVC
actions under the `[OutcomeResult]` filter, annotate the same way with
`[ProducesResponseType(typeof(SuccessResponse<Product>), StatusCodes.Status200OK)]`
when you want the same documentation.

---

## Failures

Failures carry a `Message` and an optional `Code`. Built-in failures know their HTTP
status:

| Failure | HTTP | Code |
| --- | --- | --- |
| `BadRequestFailure` | 400 | `bad_request` |
| `UnauthorizedFailure` | 401 | `unauthorized` |
| `ForbiddenFailure` | 403 | `forbidden` |
| `NotFoundFailure` | 404 | `not_found` |
| `ConflictFailure` | 409 | `conflict` |
| `ValidationFailure` | 400 | `validation` |
| `InternalServerErrorFailure` | 500 | `internal_server_error` |

### Returning a failure from a service

A failure is a return value, like any other. Three shapes cover most code:

```csharp
// 1. Null check in one expression. The implicit conversion lifts the failure.
public Outcome<Product> GetProduct(Guid id) =>
    _repo.GetById(id) ?? new ProductNotFound(id);

// 2. Guard clauses. Each check returns its own failure, success falls through.
public Outcome<Order> PlaceOrder(Cart cart)
{
    if (cart.Items.Count == 0)
        return new EmptyCart();

    if (_payments.GetBalance(cart.UserId) < cart.Total)
        return new InsufficientFunds(cart.Total);

    return _orders.Place(cart);        // implicit success
}

// 3. Explicit factories, for built-in failures or a typed outcome variable.
public Outcome<Unit> DeleteProduct(Guid id)
{
    if (!_repo.Delete(id))
        return Outcome.Fail(new NotFoundFailure($"Product '{id}' not found."));

    return Outcome.Success();
}
```

The endpoint never distinguishes these: `ToResult` sees a `Failure` and maps it to the
error envelope the same way every time.

Custom failures, two approaches:

**`IHttpFailure`** on the failure class (registration-free):

```csharp
public sealed class ProductNotFound : Failure, IHttpFailure
{
    public ProductNotFound(string message) : base(message, "product_not_found") { }

    public FailureDetails ToDetails() => new(StatusCodes.Status404NotFound, Message, Code);
}
```

**`MapFailure<T>`** at startup:

```csharp
options.Error.MapFailure<QuotaExceededFailure>(f =>
    new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));
```

Resolution order: `MapFailure<T>`, then `IHttpFailure`, then a 500 fallback. The
resolution never throws.

---

## IHttpResultFactory

Inject `IHttpResultFactory` into endpoints or services:

| Helper | Behavior |
| --- | --- |
| `http.Ok()` | 200, no payload |
| `http.Ok(data)` | 200, wrapped per `Success.Enabled` |
| `http.Ok(data, meta)` | 200 with the app's response metadata |
| `http.Created(uri, data)` | 201 with a `Location` header |
| `http.NoContent()` | 204 |
| `http.BadRequest(...)` / `http.NotFound(...)` / `http.Conflict(...)` | Error envelope |
| `http.FromFailure(failure)` | Map any failure to its response |

### Outcome extensions

```csharp
// Minimal API
result.ToResult(http, order => http.Created($"/orders/{order.Id}", order));
await outcomeTask.ToResultAsync(http, order => http.Ok(order));

// MVC
result.ToActionResult(http, order => http.Created($"/orders/{order.Id}", order));
await outcomeTask.ToActionResultAsync(http, order => http.Ok(order));

// Failure only
failure.ToErrorResponse(http);
```

### Manual Match (alternative)

Full control over both branches, no extension methods:

```csharp
app.MapGet("/", (IProductService service, IHttpResultFactory http) =>
{
    var result = service.GetProducts();
    return result.Match(
        onSuccess: page => http.Ok(page.Items),
        onFailure: failure => failure.ToErrorResponse(http));
});
```

Same result as `result.ToResult(http, page => http.Ok(page.Items))`. Pick whichever
reads clearer in context.

---

## MVC controllers

### Pattern 1: `[OutcomeResult]` filter

Return `Outcome<T>` directly; the filter maps it to HTTP:

```csharp
[ApiController, Route("products")]
[OutcomeResult]
public class ProductsController : ControllerBase
{
    [HttpGet("{id:guid}")]
    public Outcome<Product> Get(Guid id) => _service.GetProduct(id);

    [HttpDelete("{id:guid}")]
    public Outcome<Unit> Delete(Guid id) => _service.DeleteProduct(id);
}
```

Register: `builder.Services.AddOutcomeMvcFilter();`

### Pattern 2: explicit `ToActionResult`

For custom success responses (201, pagination):

```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
{
    var result = await _service.CreateProduct(request);
    return result.ToActionResult(_http, product => _http.Created($"/products/{product.Id}", product));
}
```

---

## Exception handling

Unhandled exceptions become the same error envelope as expected failures:

```csharp
builder.Services.AddOutcome(options =>
{
    options.Error.MapException<DomainException>(ex =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, ex.Message, ex.Code));
    options.Error.MapException<ValidationException>(ex =>
        new FailureDetails(StatusCodes.Status400BadRequest, "Validation Error",
            code: "validation_error",
            errors: ex.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));
    options.Error.IncludeExceptionDetails = builder.Environment.IsDevelopment();
});

var app = builder.Build();
app.UseOutcomeExceptionHandler();   // BEFORE routing
```

---

## Rate limiting

Middleware outside `IHttpResultFactory` (the ASP.NET rate limiter, for example) can
still emit your error envelope. Resolve `OutcomeOptions` from the request's services,
build a `FailureDetails`, and execute `ErrorEnvelopeResult` yourself:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        var resultOptions = context.HttpContext.RequestServices
            .GetRequiredService<OutcomeOptions>();

        var details = new FailureDetails(
            StatusCodes.Status429TooManyRequests,
            message: "Too many requests. Try again later.",
            code: "rate_limit_exceeded");

        await new ErrorEnvelopeResult(details, resultOptions)
            .ExecuteAsync(context.HttpContext, cancellationToken);
    };

    options.AddPolicy("fixed", _ => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: "global",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
        }));
});
```

A 429 from the limiter comes back as your `ErrorResponse` (or `LegacyError`, whichever
you registered), not as a bare status line. Same envelope everywhere, including the
pipeline stages that never touch `IHttpResultFactory`.

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
