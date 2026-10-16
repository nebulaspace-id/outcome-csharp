oke<div align="center">

# NebulaSpace Outcome

**A dependency-free result pattern for .NET with an ASP.NET Core integration that turns outcomes into clean HTTP responses.**

[![NuGet][outcome-shield]][outcome-nuget]
[![NuGet][aspnetcore-shield]][aspnetcore-nuget]
[![GitHub][github-shield]][github]
[![MIT License][license-shield]][license]

`Outcome<T>` · `Failure` · `IHttpResultFactory` · Success/Error envelopes · exception mapping

</div>

---

## Install

```bash
dotnet add package NebulaSpace.Outcome
dotnet add package NebulaSpace.Outcome.AspNetCore
```

---

## Core types

`Outcome<T>` is an immutable struct — either a **success** (with a `Value`) or a **failure** (with a `Failure`).

```csharp
Outcome<string> ok     = Outcome.Success("hello");
Outcome<Unit> done     = Outcome.Success();                  // no payload
Outcome<Order> order   = new Order(...);                     // implicit success
```

| Member | Description |
| --- | --- |
| `IsSuccess` / `IsFailure` | Which branch |
| `Value` / `Failure` | Access payload or error |
| `Match(onSuccess, onFailure)` | Collapse both branches into one value |

---

## Failures

Failures carry a `Message` and optional `Code`. Built-in failures know their HTTP status:

| Failure | HTTP | Code |
| --- | --- | --- |
| `BadRequestFailure` | 400 | `bad_request` |
| `UnauthorizedFailure` | 401 | `unauthorized` |
| `ForbiddenFailure` | 403 | `forbidden` |
| `NotFoundFailure` | 404 | `not_found` |
| `ConflictFailure` | 409 | `conflict` |
| `ValidationFailure` | 400 | `validation` |
| `InternalServerErrorFailure` | 500 | `internal_server_error` |

Custom failures — two approaches:

**`MapFailure<T>`** — register at startup:

```csharp
options.Error.MapFailure<QuotaExceededFailure>(f =>
    new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));
```

**`IHttpFailure`** — failure carries its own HTTP intent:

```csharp
public sealed class QuotaExceededFailure : Failure, IHttpFailure
{
    public QuotaExceededFailure(string message) : base(message, "quota_exceeded") { }
    public FailureDetails ToDetails() =>
        new(StatusCodes.Status422UnprocessableEntity, Message, Code);
}
```

Resolution order: `MapFailure<T>` > `IHttpFailure` > 500 fallback (never throws).

---

## Your types (required)

The package ships `Default*` types for testing and demo purposes only. **In production, define your own** success, error, and pagination types to get the exact response shape your API needs.

### Error envelope

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
        StatusCode = details.StatusCode, Code = details.Code,
        Message = details.Message, TraceId = context.TraceIdentifier, Errors = details.Errors,
    };
}
```

### Success envelope

One envelope type per payload, metadata typed however you like:

```csharp
public sealed class SuccessResponse<TData> : ISuccessEnvelope<TData>
{
    public int StatusCode { get; init; }
    public TData? Data { get; init; }
    public Meta? Meta { get; init; }        // your own type, narrowed from object

    public static ISuccessEnvelope<TData> From(
        TData data, object? meta, HttpContext httpContext, int statusCode)
        => new SuccessResponse<TData>
        {
            StatusCode = statusCode, Data = data,
            // Library hands meta over as object — it must not know your meta type.
            Meta = meta as Meta,
        };
}
```

`Meta` arrives as `object` because its shape is your app's business: the library never
inspects it, and `System.Text.Json` serializes it by its runtime type. Narrow it to your
own type (`meta as Meta`) and Swagger documents the exact fields instead of an opaque
blob. Want meta on every response? Narrow it with a fallback — endpoints then opt into
*pagination*, not into meta:

```csharp
Meta = meta as Meta ?? new Meta(),
```

**No separate no-payload envelope.** `http.Ok()` (no arguments) is the same envelope with
a null payload: `T` closes as `object`, `Data` stays `null`, and
`JsonIgnoreCondition.WhenWritingNull` omits the key.

```csharp
options.Success.SetResponse(typeof(SuccessResponse<>));    // one type, every response
```

### Metadata (`Meta`)

Metadata is one type for the whole API — build it in the endpoint layer and hand it to
`http.Ok(data, meta)`. Pagination follows the JSON:API convention: the fields sit
**directly on** `meta`, not nested under a `pagination` property:

```json
{
  "data": [ ... ],
  "meta": {
    "page": 1, "limit": 10, "totalItems": 100, "totalPages": 10, "hasMore": true,
    "timestamp": "2026-01-01T00:00:00+00:00"
  }
}
```

```csharp
public sealed class Meta
{
    // Offset pagination — null when the response isn't offset-paged
    public int? Page { get; init; }
    public int? Limit { get; init; }
    public long? TotalItems { get; init; }
    public int? TotalPages { get; init; }

    public bool? HasMore { get; init; }               // offset and cursor

    // Cursor pagination — null when the response isn't cursor-paged
    public string? PreviousCursor { get; init; }
    public string? NextCursor { get; init; }

    // Default value — no builder needed, the timestamp is there even for `new Meta()`.
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    // One With... method per pagination strategy flattens that strategy's fields.
    public Meta WithOffsetPagination(OffsetPagintion p) => new()
    {
        Page = p.Page, Limit = p.Limit, TotalItems = p.TotalItems,
        TotalPages = p.TotalPages, HasMore = p.HasMore,
    };
}

// Endpoint — offset-paged
Outcome.Success(service.GetProducts(query)).ToResult(http, page =>
    http.Ok(page.Items, new Meta().WithOffsetPagination(page.ToOffsetPagination(query))));

// Endpoint — not paginated: no pagination fields, just the timestamp
http.Ok(product, new Meta());
```

Every pagination field is nullable, so `JsonIgnoreCondition.WhenWritingNull` omits the
ones a strategy doesn't fill — an unpaged response emits just `timestamp`, and offset
and cursor responses each emit their own subset.

### Pagination types

Build your own query, result, and info types for each strategy. The library ships no pagination caps — the examples below show all three.

Example — custom offset pagination:

```csharp
// 1. Query — bound from query string via [AsParameters]
public sealed class OffsetQuery : IPaginatedQuery
{
    private int _page = 1;
    private int _limit = 10;

    public int Page  { get => _page;  set => _page  = Math.Max(1, value); }
    public int Limit { get => _limit; set => _limit = Math.Max(1, value); }
    public int Offset => (Page - 1) * Limit;
}

// 2. Result — returned by the service layer
public sealed class OffsetResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public long TotalCount { get; set; }

    public OffsetPagination ToOffsetPagination(OffsetQuery query) =>
        OffsetPagination.Create(query, (int)TotalCount);
}

// 3. Info — response metadata implementing IPaginationDetails
public sealed class OffsetPagination : IPaginationDetails
{
    public bool HasMore => Page < TotalPages;
    public int Page { get; set; }
    public int Limit { get; set; }
    public int TotalPages { get; set; }
    public long? TotalItems { get; set; }

    public static OffsetPagination Create(OffsetQuery query, int totalItems) => new()
    {
        Page = query.Page, Limit = query.Limit,
        TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / query.Limit),
        TotalItems = totalItems,
    };
}

// 4. Endpoint — one-line call; the endpoint builds the app's Meta
group.MapGet("/", (IProductService service, [AsParameters] OffsetQuery query, IHttpResultFactory http, HttpContext context) =>
    Outcome.Success(service.GetProducts(query)).ToResult(http, page =>
        http.Ok(page.Items, new Meta()
            .WithOffsetPagination(page.ToOffsetPagination(query))))
    .Produces<SuccessResponse<Product[]>>();
```

Example — custom cursor pagination:

```csharp
// 1. Query
public sealed class CursorQuery : IPaginatedQuery
{
    private string? _cursor;
    private int _limit = 10;

    public string? Cursor { get; set; }
    public int Limit { get => _limit; set => _limit = Math.Max(1, value); }
}

// 2. Result — service returns this with opaque cursors
public sealed class CursorResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public string? PreviousCursor { get; set; }
    public string? NextCursor { get; set; }
    public bool HasMore => NextCursor is not null;
    public long? TotalCount { get; set; }

    public CursorPagination ToCursorPagination() => CursorPagination.Create(this);
}

// 3. Info
public sealed class CursorPagination : IPaginationDetails
{
    public string? PreviousCursor { get; set; }
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }
    public long? TotalItems { get; set; }

    public static CursorPagination Create<T>(CursorResult<T> result) => new()
    {
        PreviousCursor = result.PreviousCursor, NextCursor = result.NextCursor,
        HasMore = result.HasMore, TotalItems = result.TotalCount,
    };
}

// 4. Endpoint
group.MapGet("/cursor", (IProductService service, [AsParameters] CursorQuery query, IHttpResultFactory http) =>
    Outcome.Success(service.GetProductsCursor(query)).ToResult(http, page =>
        http.Ok(page.Items, new Meta()
            .WithCursorPagination(page.ToCursorPagination()))))
    .Produces<SuccessResponse<Product[]>>();
```

---

## Register

```csharp
builder.Services.AddOutcome(options =>
{
    options.Success.SetResponse(typeof(SuccessResponse<>));    // one type, every response
    options.Error.SetResponse<ErrorResponse>();
});
```

---

## IHttpResultFactory

Inject `IHttpResultFactory` into endpoints or services to build responses:

| Helper | Behavior |
| --- | --- |
| `http.Ok()` | 200, no payload |
| `http.Ok(data)` | 200, wrapped per `Success.Enabled` |
| `http.Ok(data, meta)` | 200 with the app's response metadata |
| `http.Created(uri, data)` | 201 with `Location` header |
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

If you prefer full control over both branches without extension methods:

```csharp
app.MapGet("/", (IProductService service, IHttpResultFactory http) =>
{
    var result = service.GetProducts();
    return result.Match(
        onSuccess: page => http.Ok(page.Items),
        onFailure: failure => failure.ToErrorResponse(http));
});
```

This is equivalent to `result.ToResult(http, page => http.Ok(page.Items))` — choose whichever reads clearer in context.

---

## MVC controllers

### Pattern 1: `[OutcomeResult]` filter

Return `Outcome<T>` directly — the filter maps it to HTTP:

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

For custom success responses (201, pagination, etc.):

```csharp
[HttpPost]
public Task<IActionResult> Create([FromBody] CreateProductRequest request) =>
    _service.CreateProduct(request)
        .ToActionResultAsync(_http, product => _http.Created($"/products/{product.Id}", product));
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
