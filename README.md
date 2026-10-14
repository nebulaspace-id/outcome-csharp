<div align="center">

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

Non-paginated:

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

Paginated (2-param generic):

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
            StatusCode = statusCode, Data = data,
            Pagination = pagination is TPage typed ? typed : default,
        };
}
```

No-payload (for `http.Ok()`):

```csharp
public sealed class SuccessResponse
{
    public int StatusCode { get; init; }
    public static object From(HttpContext httpContext, int statusCode) =>
        new SuccessResponse { StatusCode = statusCode };
}
```

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

// 4. Endpoint — one-line call
group.MapGet("/", (IProductService service, [AsParameters] OffsetQuery query, IHttpResultFactory http) =>
    Outcome.Success(service.GetProducts(query)).ToResult(http, page =>
        http.Ok(page.Items, page.ToOffsetPagination(query))))
    .Produces<SuccessResponse<Product[], OffsetPagination>>();
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
        http.Ok(page.Items, page.ToCursorPagination())))
    .Produces<SuccessResponse<Product[], CursorPagination>>();
```

---

## Register

```csharp
builder.Services.AddOutcome(options =>
{
    options.Success.SetResponse(typeof(SuccessResponse<>));    // non-paginated
    options.Success.SetResponse(typeof(SuccessResponse<,>));   // paginated
    options.Success.SetResponse(typeof(SuccessResponse));      // no-payload
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
| `http.Ok(data, pagination)` | 200 with pagination metadata |
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
