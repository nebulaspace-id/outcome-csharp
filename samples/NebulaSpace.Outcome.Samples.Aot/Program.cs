// NebulaSpace.Outcome — NativeAOT smoke test.
//
// This sample uses ONLY the AOT-safe configuration surface, to prove the library
// publishes and runs under NativeAOT with no reflection at runtime:
//   - success envelope via the delegate overload (paginated + non-paginated)
//   - no-payload envelope via the delegate overload
//   - error envelope via the generic SetResponse<TEnvelope>()
//   - pagination query types via RegisterQuery<T>(), with ScanAssemblies = false
//   - failure mapping via MapFailure<T> (plain dictionaries)
//
// Note: no MVC controllers here — ASP.NET Core MVC itself is not NativeAOT-supported
// (IL2026), so a controller sample cannot publish AOT regardless of the library.
// The [OutcomeResult] filter is still reflection-free, but use Minimal APIs for AOT.
//
// Publish: dotnet publish -c Release -r win-x64 --self-contained
// (NativeAOT needs the C++ linker / clang on the build machine.)

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Extensions;
using NebulaSpace.Outcome.AspNetCore.Failures;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Pagination;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddOutcome(options =>
{
    // Success envelope — delegate overload. Handles paginated and non-paginated
    // responses; pagination arrives as the second argument. AOT-safe.
    options.Success.SetResponse((data, pagination, http, statusCode) =>
        new SuccessResponse
        {
            StatusCode = statusCode,
            Data = data,
            Pagination = pagination,
        });

    // No-payload success (http.Ok() with no args) — delegate overload.
    options.Success.SetResponse((http, statusCode) =>
        new SuccessResponse { StatusCode = statusCode });

    // Error envelope — generic form dispatches via static abstract, AOT-safe.
    options.Error.SetResponse<ErrorResponse>();

    // Pagination caps. These are plain properties — still work under AOT. They are
    // copied to each query type's static fields by Configure, dispatched through
    // RegisterQuery<T>() below (no assembly scan, no reflection).
    options.Pagination.MaxPage = 25;
    options.Pagination.MaxLimit = 50;

    // AOT-safe pagination discovery: disable the reflection-based assembly scan and
    // register each query type explicitly.
    options.Pagination.ScanAssemblies = false;
    options.Pagination.RegisterQuery<CatalogQuery>();

    // Failure mapping — plain dictionaries, no reflection.
    options.Error.MapFailure<RateLimitFailure>(f =>
        new FailureDetails(StatusCodes.Status429TooManyRequests, f.Message, f.Code));
});

var app = builder.Build();

app.UseOutcomeExceptionHandler();   // BEFORE routing

// ── Endpoints ────────────────────────────────────────────────────────────────

// Non-paginated success through the delegate envelope.
app.MapGet("/greet/{name}", (string name, IHttpResultFactory http) =>
    http.Ok(new { greeting = $"Hello, {name}!" }));

// No payload — status-only envelope.
app.MapGet("/ping", (IHttpResultFactory http) => http.Ok());

// Paginated — CatalogQuery is bound via [AsParameters] and configured by RegisterQuery.
// The caps (MaxPage=25, MaxLimit=50) live in CatalogQuery.MaxPage/MaxLimit.
app.MapGet("/catalog", ([AsParameters] CatalogQuery query, IHttpResultFactory http) =>
{
    var items = Enumerable.Range(query.Offset, Math.Min(query.Limit, 3))
        .Select(i => new Product(i))
        .ToArray();

    return http.Ok(items, new CatalogInfo
    {
        Page = query.Page,
        Limit = query.Limit,
        TotalItems = 100,
    });
});

// Failure via a self-mapping built-in.
app.MapGet("/missing", (IHttpResultFactory http) =>
    http.FromFailure(new NotFoundFailure("No such resource.")));

// Failure via MapFailure<T>.
app.MapGet("/limited", (IHttpResultFactory http) =>
    http.FromFailure(new RateLimitFailure("Slow down.")));

// Unhandled exception → the same error envelope, via the exception middleware.
app.MapGet("/boom", (IHttpResultFactory http) =>
{
    throw new InvalidOperationException("Boom");
});

app.Run();

// ── Types ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Delegate envelope. <c>Data</c> and <c>Pagination</c> are typed <c>object</c>, not
/// <c>IPaginationDetails</c>: STJ serializes an object-typed property by its runtime type,
/// so the JSON keeps the full payload and pagination shape.
/// </summary>
public sealed class SuccessResponse
{
    public int StatusCode { get; init; }
    public object? Data { get; init; }
    public object? Pagination { get; init; }
}

/// <summary>App error envelope — serialized as the body for every failed response.</summary>
public sealed class ErrorResponse : IErrorEnvelope
{
    public int? StatusCode { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }
    public string? TraceId { get; init; }

    public static IErrorEnvelope From(FailureDetails details, HttpContext context) => new ErrorResponse
    {
        StatusCode = details.StatusCode,
        Code = details.Code,
        Message = details.Message,
        TraceId = context.TraceIdentifier,
    };
}

/// <summary>
/// Custom pagination query, registered explicitly via
/// <c>options.Pagination.RegisterQuery&lt;CatalogQuery&gt;()</c>. <c>Configure</c> copies the
/// app caps (<c>MaxPage</c>, <c>MaxLimit</c>) into the static fields — static interface
/// dispatch, no reflection.
/// </summary>
public sealed class CatalogQuery : IPaginatedQuery
{
    public static int MaxPage = int.MaxValue;
    public static int MaxLimit = int.MaxValue;

    private int _page = 1;
    private int _limit = 10;

    public int Page { get => _page; set => _page = Math.Clamp(value, 1, MaxPage); }
    public int Limit { get => _limit; set => _limit = Math.Clamp(value, 1, MaxLimit); }
    public int Offset => (Page - 1) * Limit;

    public static void Configure(PaginationOptions options)
    {
        MaxPage = Math.Max(1, options.MaxPage);
        MaxLimit = Math.Max(1, options.MaxLimit);
    }
}

/// <summary>Pagination metadata sent in the success envelope.</summary>
public sealed class CatalogInfo : IPaginationDetails
{
    public int Page { get; init; }
    public int Limit { get; init; }
    public long? TotalItems { get; init; }
}

/// <summary>Payload type for the catalog endpoint.</summary>
public sealed record Product(int Id);

/// <summary>An app failure mapped to 429 via MapFailure at startup.</summary>
public sealed class RateLimitFailure : Failure
{
    public RateLimitFailure(string message) : base(message, "rate_limited") { }
}
