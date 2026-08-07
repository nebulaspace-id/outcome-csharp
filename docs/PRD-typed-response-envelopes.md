# PRD — Typed Response Envelopes

**Branch:** `feature/typed-response-envelopes`
**Version target:** `v0.2.0` (breaking)
**Status:** Implemented

## 1. Problem statement

Today the error envelope is shaped at runtime from a **string-keyed dictionary**:

```csharp
options.ErrorEnvelope.Add("traceId", context => context.TraceIdentifier);
options.ErrorEnvelope.Add("environment", _ => builder.Environment.EnvironmentName);
```

The keys are plain strings and the values are built at request time and flattened into
the JSON body via `[JsonExtensionData]`. This is invisible to OpenAPI/Swagger:

- **No type safety** — a typo in a key (`"traceid"` vs `"traceId"`) silently changes the wire body.
- **No discoverability** — the shape of an error response is decided at runtime inside a dictionary, so nothing documents it.
- **No `Produces<T>`** — endpoints can't document their error shape with `.Produces<...>()`, because there is no type to name.

The user should be able to shape both the success and error responses with **their own
wrapper types**, and document them **typesafely**:

```csharp
.Produces<SuccessResponse<Data>>()   // what a successful response actually serializes
.Produces<ErrorResponse>()            // what an error response actually serializes
```

## 2. Goals / non-goals

### Goals
- **G1.** Success responses are shaped by a typed envelope (`SuccessEnvelope`) — a library default that can be replaced.
- **G2.** Error responses are shaped by a typed envelope (`ErrorEnvelope`) — any user type, **no inheritance required**.
- **G3.** The serialized body **IS** the configured type — so `.Produces<T>()` documents exactly what hits the wire.
- **G4.** Per-request fields (`traceId`, `path`, custom) are **real C# properties**, not runtime dictionary keys.
- **G5.** HTTP status comes from the failure's **intent** (`FailureDetails.StatusCode`), decoupled from the body shape.
- **G6.** Exception mappers return **intent** (`FailureDetails`), not envelopes — so one typed error shape renders everywhere (expected failures, mapped failures, and unhandled exceptions).

### Non-goals
- **NG1.** No support for string-keyed envelope properties (`ErrorEnvelope.Add(key, factory)`) — removed in this version.
- **NG2.** No DI-replaceable strategy interface (`IErrorResponseFactory`) — removed in this version.
- **NG3.** No base-class requirement for custom envelopes (no subclassing `ProblemDetails`).
- **NG4.** No controller/MVC-specific support — Minimal APIs are the target.

## 3. User stories / acceptance criteria

### Story 1 — "I want a completely custom error shape"
As an API author, I want my error body to be **any** class I define (no base class), so
I control exactly what clients see.

```csharp
public sealed class ApiError
{
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? TraceId { get; set; }
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; set; }

    public static ApiError From(FailureDetails details, HttpContext context) => new()
    {
        Code = details.Code,
        Message = details.Message,
        TraceId = context.TraceIdentifier,
        FieldErrors = details.Errors,
    };
}
```

Configure once at startup:

```csharp
builder.Services.AddOutcomeHttp(options =>
{
    options.ErrorEnvelope.Set(ErrorResponse.From);
});
```

**AC1.** `options.ErrorEnvelope.Set<TEnvelope>` accepts any type; it is serialized as-is.
**AC2.** `FailureDetails` supplies the intent: `StatusCode` → HTTP status; `Message`/`Code`/`Errors` → body fields.
**AC3.** No `IErrorResponse`, no `IErrorResponseFactory`, no `DefaultErrorResponseFactory` exist.

### Story 2 — "Document the response typesafely"
As an API author, I want to document my responses with `.Produces<T>()`, and the docs
must match what the library actually serializes.

```csharp
.MapPost("/", ...)
    .Produces<DefaultSuccessResponse<Order>>(StatusCodes.Status201Created)
    .Produces<ApiError>(StatusCodes.Status400BadRequest)
    .Produces<ApiError>(StatusCodes.Status422UnprocessableEntity);
```

**AC4.** The Swagger schema for the error response lists exactly `ApiError`'s properties.
**AC5.** The Swagger schema for the success response lists exactly the envelope's properties
(`statusCode`, `data`, `pagination`).

*(Verified in the sample: `/swagger/v1/swagger.json` documents `code`/`message`/`traceId`/
`requestId`/`fieldErrors` for `ApiError`, and `statusCode`/`data`/`pagination` for the
success envelope.)*

### Story 3 — "Same error shape everywhere"
As an API author, I want expected failures, mapped failures, and unhandled exceptions to
all render through the **one** configured error envelope.

```csharp
// Expected failures (from an Outcome) — same ApiError.
result.ToHttpResult(http, order => http.Created(order, o => o.Id));

// Mapped failures — return HTTP intent:
options.Failures.Map<QuotaExceededFailure>(f =>
    new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));

// Unhandled exceptions — exception mappers also return intent:
options.Exceptions.Map<DomainException>(ex =>
    new FailureDetails(StatusCodes.Status422UnprocessableEntity, ex.Message, ex.Code));
```

**AC6.** A mapped failure renders the configured envelope (e.g. `ApiError`), not a different shape.
**AC7.** A mapped exception renders the configured envelope too.

## 4. Design

### 4.1 Layer separation

```
 failure / exception
        │   (intent)
        ▼
   FailureDetails ── StatusCode ──▶ HTTP status header
   (StatusCode,      Message ─┐
    Message,          Code   ─┼──▶ ErrorEnvelope factory ──▶ body JSON
    Code,             Errors ─┘
    Errors)
```

- **`FailureDetails`** is the *intent* — it is **never serialized**.
- The **error envelope** is the *representation* — either the default (`DefaultProblemDetails`)
  or the user's type configured via `Use<T>`.
- The **HTTP status** always comes from `FailureDetails.StatusCode`, never from the envelope.

### 4.2 Error envelope — the typed configuration API

`ErrorEnvelopeOptions` (in `NebulaSpace.Outcome.AspNetCore.Options`):

```csharp
public sealed class ErrorEnvelopeOptions
{
    internal Type? EnvelopeType { get; private set; }
    internal Func<FailureDetails, HttpContext, object>? Factory { get; private set; }

    public void Use<TEnvelope>(Func<FailureDetails, HttpContext, TEnvelope> factory)
    {
        EnvelopeType = typeof(TEnvelope);
        Factory = (details, ctx) => factory(details, ctx)!;
    }
}
```

- `EnvelopeType` is retained so the schema can be documented without instantiating.
- The factory receives the failure's intent + the current `HttpContext` (for `traceId`,
  `path`, headers, etc.).

`ErrorEnvelopeResult` (`Http` namespace) writes it:

```csharp
public async Task ExecuteAsync(HttpContext httpContext)
{
    object envelope = _options.ErrorEnvelopeFactory is { } factory
        ? factory(_details, httpContext)
        : BuildDefaultProblemDetails(_details);

    httpContext.Response.StatusCode = _details.StatusCode;
    await httpContext.Response.WriteAsJsonAsync(envelope);
}
```

### 4.3 The default error shape — `DefaultProblemDetails`

When nothing is configured, the library writes clean RFC 7807:

```csharp
public sealed class DefaultProblemDetails
{
    public string Title { get; set; } = string.Empty;   // reason phrase, e.g. "Not Found"
    public int Status { get; set; }                      // = FailureDetails.StatusCode
    public string Detail { get; set; } = string.Empty;   // = FailureDetails.Message ?? ""
}
```

All non-nullable → clean OpenAPI schema (no `nullable` flags). It has **no** `type`,
`traceId`, `path`, `statusCode`, or `errorCode` — RFC 7807 semantics only.

> `FailureDetails` and `DefaultProblemDetails` are **not** redundant. `FailureDetails`
> is the intent/input (never serialized, and it carries `Code`/`Errors` that a clean
> RFC 7807 body doesn't show). `DefaultProblemDetails` is the serialization view of the
> default body — the type you'd put in `.Produces<DefaultProblemDetails>()` when
> unconfigured. `FailureDetails` can never be that type, because it lacks a `Title`
> and would leak intent-only fields.

### 4.4 The default success shape — `DefaultSuccessResponse<T>`

```csharp
public sealed class DefaultSuccessResponse<T>
{
    public int StatusCode { get; set; }
    public T? Data { get; set; }
    public PageInfo? Pagination { get; set; }

    public static DefaultSuccessResponse<T> Ok(T? data, PageInfo? pagination = null) => ...;
    internal static DefaultSuccessResponse<T> Created(T data) => ...;
}
```

Disabled via `options.SuccessEnvelope.Enabled = false` to return the raw payload.

### 4.5 Mappers return intent

`FailureMappingOptions.Map<TFailure>(Func<TFailure, FailureDetails>)` and
`ExceptionMappingOptions.Map<TException>(Func<TException, FailureDetails>)` both return
`FailureDetails` — never envelopes. The configured envelope renders the intent, so
**one error shape renders everywhere** (expected, mapped, unhandled).

### 4.6 What changed

| Area | Before (v0.1.0) | After (v0.2.0) |
|---|---|---|
| Error envelope config | `ErrorEnvelope.Add("key", factory)` (string keys) | `ErrorEnvelope.Set<TEnvelope>(factory)` (typed) |
| Envelope type | runtime dictionary flattened via `[JsonExtensionData]` | any CLR type, serialized as-is |
| Error intent | built inside the envelope factory | `FailureDetails` (StatusCode/Message/Code/Errors) |
| Failure mapper return | envelope | `FailureDetails` |
| Exception mapper return | envelope | `FailureDetails` |
| Success envelope | `SuccessResponse<T>` | `DefaultSuccessResponse<T>` |
| Default error body | `OutcomeProblemDetails` (with `Extensions`) | `DefaultProblemDetails` (clean RFC 7807, non-nullable) |
| Removed | `IErrorResponse`, `IErrorResponseFactory`, `DefaultErrorResponseFactory`, `ErrorResponse`, `ResponseBody` | — |

## 5. Breaking changes / migration

This is a **breaking change**, released as **v0.2.0**.

| Removed | Migration |
|---|---|
| `ErrorEnvelope.Add(string, Func<HttpContext, object?>)` | `ErrorEnvelope.Set<TEnvelope>((details, ctx) => ...)` |
| `IErrorResponse` / `IErrorResponseFactory` / `DefaultErrorResponseFactory` | none — no DI swap needed; use `Use<T>` |
| `ErrorResponse` | define your own body type, or keep the default |
| `ResponseBody` | define your own body type, or keep the default |
| `OutcomeProblemDetails` (and its `Extensions`) | use `DefaultProblemDetails` or `Use<T>` |

## 6. OpenAPI wiring (verified)

The sample (`samples/NebulaSpace.Outcome.Samples.WebApi`) was run and its
`/swagger/v1/swagger.json` inspected. `.Produces<T>()` documents exactly what the library
serializes:

- `GET /greet/{name}` → `200` `DefaultSuccessResponse<GreetingResponse>` (`statusCode`/`data`/`pagination`), `400` `ApiError` (`code`/`message`/`traceId`/`requestId`/`fieldErrors`).
- `POST /orders` → `201` `DefaultSuccessResponse<Order>`, `400`/`422` `ApiError`.
- `GET /orders` → `200` `DefaultSuccessResponse<Order[]>`, `400` `ApiError`.

## 7. Testing

- **Core** — 20 unit tests (unchanged).
- **AspNetCore unit** — 69 tests. Key coverage:
  - default envelope = clean RFC 7807 (`title`/`status`/`detail`), no `traceId`/`path`/`type`/`statusCode`/`errorCode`;
  - typed envelope serialized as the user type, with per-request fields (`traceId`, `path`) and field errors;
  - status comes from `FailureDetails.StatusCode`, not the envelope;
  - `Detail` is `""` when no message (non-nullable default);
  - mapped failures/exceptions render through the configured envelope.
- **AspNetCore integration** — 9 tests, incl. `UseOutcomeExceptionHandler` rendering the typed envelope with `message`/`code` for mapped exceptions, and safe fallback for unhandled exceptions (no details leaked unless configured).

## 8. Open items / future work

- **Controllers/MVC**: `DefaultProblemDetails` is Minimal-API-first; a controller story could reuse the same envelope via an MVC `ProblemDetails`-style integration.
- **`DefaultProblemDetails` serialization options**: the empty `detail` string currently always serializes (non-nullable). If a "omit empty detail" behavior is wanted later, it'd be a config option on `DefaultProblemDetails`, not a nullability change.
- **Documentation**: update the README's ASP.NET Core section to lead with the typed-envelope story (already partially done in this branch).
