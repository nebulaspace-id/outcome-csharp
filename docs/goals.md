# Hardening Goals — `fix/audit-hardening`

Branch: `fix/audit-hardening`
Scope: audit findings P0 (security/correctness), P1 (design/pattern), P2 (DX/packaging).
Out of scope: GitHub Actions CI workflow (explicitly deferred).

## P0 — Security & correctness

- [x] `Outcome<T>` never reports a failure without a `Failure`; `default(Outcome<T>)` throws a clear error instead of an NRE.
- [x] `Outcome<T>.Fail(null)` is rejected. `null` is a valid success payload, so the implicit conversion from `null` succeeds; `Outcome<Product?>` is the signature that states null is intended.
- [x] Pagination arithmetic cannot overflow (`long` math, validated bounds); default query types clamp page/limit to safe caps.
- [x] Unknown failure → HTTP 500 does not expose the internal failure message.
- [x] Exception middleware: client aborts rethrow; mapped exceptions logged as warnings; `BadHttpRequestException` maps to its status; CORS/Vary headers survive reset.
- [x] `FailureDetails` rejects status codes outside 100–999.
- [x] Reflection-based envelope registration fails fast at startup, never at request time.
- [x] `Created` validates the `Location` value (no empty/CRLF header injection).
- [x] Base-type mappers actually match: `MapFailure<Failure>` and `MapException<Exception>` are consulted for derived types instead of stopping before the base class.
- [x] A successful `Outcome<Unit>` cannot be lifted into `Outcome<T>` with a null/default payload — "no payload" is not the same as "payload is null", so the conversion throws with guidance.

## P1 — Design & API

- [x] Pagination types kept (no breaking removals); `Default*` queries capped (`MaxPage`, `MaxLimit`) and pagination math uses 64-bit arithmetic.
- [x] `[OutcomeResult]` maps successful `Outcome<Unit>` to 204, matching docs.
- [x] Per-request reflection replaced with a cached compiled delegate.
- [x] `IOutcome.Failure` is nullable and honest; `TryGetValue` / `TryGetFailure` added.
- [x] Implementation types are internal (`ErrorEnvelopeResult`, `SuccessEnvelopeResult<T>`, `OutcomeActionResult`, `HttpResultFactory`); public surface is the interfaces + extensions. `CreatedResult<T>` folded into `SuccessEnvelopeResult<T>` via `locationUri`.
- [x] Options are frozen after `AddOutcome`; post-startup mutation throws.
- [x] `UseOutcomeExceptionHandler` fails fast when `AddOutcome` was not called.
- [x] Every response uses the configured envelopes: `AddOutcomeMvcFilter()` maps model-state 400s, `UseOutcomeStatusCodePages()` maps routing 404/405/415, auth 401/403, and rate-limiter 429 with overridable `MapStatusCode` defaults.

## P2 — Developer experience

- [x] XML docs, PDB + `.snupkg`, SourceLink enabled; `GenerateDocumentationFile=true`.
- [x] ASP.NET Core package multi-targets `net8.0;net9.0;net10.0`; fake TFM metadata removed.
- [x] Independent package versions (`NebulaSpace.Outcome` 0.5.0, `NebulaSpace.Outcome.AspNetCore` 0.8.0), `global.json`, `.editorconfig`, warnings-as-errors in `script.sh`; SourceLink bumped past the `NU1902` advisory.
- [x] Empty integration test project removed; test/sample dependencies aligned.
- [x] README examples compile; stale claims removed; Unit/204, error-shape, pagination-cap, and rate-limiter guidance updated.
- [x] Sample: thread-safe in-memory repository (lock + no TOCTOU), per-IP rate-limit partition with `Retry-After`, validation returns a failure value, single OpenAPI stack (Swashbuckle only), middleware order fixed, `ProductService` scoped to match FluentValidation.
- [x] Sample: duplicate-name check moved into the repository lock (`Add` returns `null` → 409, no 422 race), pagination math reuses `DefaultOffsetPagination`, plus a sample `README.md` and a `.http` request collection for every response shape.

## Deviations

- Pagination namespace kept in full (user decision): all `Default*` types and the two marker interfaces remain; only bugs were fixed.
- No `.github/workflows` added (explicitly out of scope).

## Verification

- `dotnet build outcome-csharp.slnx -c Release -warnaserror` → 0 warnings, 0 errors (all TFMs).
- `dotnet test outcome-csharp.slnx -c Release` → 211 tests, all passing (34 core, 164 ASP.NET Core unit, 13 integration).
- `dotnet pack` → `NebulaSpace.Outcome.0.5.0` + `NebulaSpace.Outcome.AspNetCore.0.8.0` (`.nupkg` + `.snupkg`).
- Sample smoke test (`dotnet run`, `http://localhost:5232`): ping 200, list 200, 404 envelope, validation 400 with field errors, create 201 + `Location`, duplicate 409, routing-driven 404/405/415 with the configured envelope, MVC model-state 400 as `validation_error`, delete 204, HEAD 404 without body, Swagger 200, MVC 200, rate limit 429 with `Retry-After` and the configured envelope.
- Regression tests cover every P0 item.
