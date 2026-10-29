using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using NebulaSpace.Outcome.AspNetCore.Extensions;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.Samples.WebApi.Features.Products;
using NebulaSpace.Outcome.Samples.WebApi.Shared;

var builder = WebApplication.CreateBuilder(args);

// ── JSON ──────────────────────────────────────────────────────────────────

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// ── Controllers (MVC) ─────────────────────────────────────────────────────

builder.Services.AddControllers();
builder.Services.AddOutcomeMvcFilter();

// ── Swagger ───────────────────────────────────────────────────────────────

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Outcome Samples — Product Catalog",
        Version = "v1",
        Description = "Demonstrates the Outcome library with a proper layered architecture.",
    });
});

// ── Rate limiting ─────────────────────────────────────────────────────────

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        // Tell a well-behaved client when to retry instead of making it guess.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        // Reuse the library's error envelope so 429 looks like every other error.
        var http = context.HttpContext.RequestServices.GetRequiredService<IHttpResultFactory>();
        var details = new FailureDetails(
            StatusCodes.Status429TooManyRequests,
            message: "Too many requests. Try again later.",
            code: "rate_limit_exceeded");

        await http.FromFailureDetails(details).ExecuteAsync(context.HttpContext);
    };

    // One bucket per client IP, not one global bucket — otherwise a single noisy
    // caller throttles everybody.
    options.AddPolicy("per-ip", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        }));
});

// ── Outcome ───────────────────────────────────────────────────────────────

builder.Services.AddOutcome(options =>
{
    // 1-param open-generic — closes T per request so Swagger documents
    // Data: Product, not Data: object. One envelope covers every response:
    // Meta rides along as object, and http.Ok() (no payload) simply has a
    // null Data that WhenWritingNull omits.
    options.Success.SetResponse(typeof(SuccessResponse<>));

    // Error envelope — compile-time safe (IErrorEnvelope is non-generic).
    options.Error.SetResponse<ErrorResponse>();

    // Map app-defined failures to HTTP intent.
    // ProductNotFound and DuplicateProductName are NOT listed here — they
    // implement IHttpFailure and self-map via ToDetails() (Open/Closed pattern).
    options.Error.MapFailure<QuotaExceededFailure>(f =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));

    // Map exceptions to HTTP intent.
    options.Error.MapException<DomainException>(ex =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, ex.Message, "domain_error"));

    // Unhandled exceptions — only leak details in Development.
    options.Error.IncludeExceptionDetails = builder.Environment.IsDevelopment();
});

// ── App services ──────────────────────────────────────────────────────────

builder.Services.AddSingleton<IProductRepository, InMemoryProductRepository>();
// Scoped: ProductService consumes FluentValidation's scoped IValidator<T>. Registering
// it as a singleton would capture the validator (the container rejects that at startup).
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductRequestValidator>(includeInternalTypes: false);

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────

// FIRST: wraps rate limiting, routing, and endpoints, so an exception anywhere
// still produces the library's error envelope.
app.UseOutcomeExceptionHandler();
app.UseRouting();
app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseSwagger();
app.UseSwaggerUI();

// ── Endpoints ─────────────────────────────────────────────────────────────

app.MapProductEndpoints();
app.MapControllers();

app.Run();
