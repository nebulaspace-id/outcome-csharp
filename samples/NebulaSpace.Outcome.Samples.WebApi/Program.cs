using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using NebulaSpace.Outcome;
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

builder.Services.AddControllers()
    .AddApplicationPart(typeof(ProductsController).Assembly);

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
        var resultOptions = context.HttpContext.RequestServices.GetRequiredService<OutcomeOptions>();
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
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        }));
});

// ── Outcome ───────────────────────────────────────────────────────────────

builder.Services.AddOutcome(options =>
{
    // 1-param open-generic — closes T per request so Swagger documents
    // Data: Product, not Data: object. Meta rides along as object, so
    // paginated and non-paginated responses share this one envelope.
    options.Success.SetResponse(typeof(SuccessResponse<>));

    // Non-generic — responses with no payload (http.Ok() with no arguments).
    options.Success.SetResponse(typeof(SuccessResponse));

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

    // FluentValidation exceptions → 400 Bad Request with field errors.
    options.Error.MapException<ValidationException>(ex =>
        new FailureDetails(
            StatusCodes.Status400BadRequest,
            message: "Validation Error",
            code: "validation_error",
            errors: ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));

    // Unhandled exceptions — only leak details in Development.
    options.Error.IncludeExceptionDetails = builder.Environment.IsDevelopment();
});

// ── App services ──────────────────────────────────────────────────────────

builder.Services.AddSingleton<IProductRepository, InMemoryProductRepository>();
builder.Services.AddSingleton<IProductService, ProductService>();
builder.Services.AddSingleton<IValidator<CreateProductRequest>, CreateProductRequestValidator>();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────

app.UseRateLimiter();
app.UseOutcomeExceptionHandler();
app.UseHttpsRedirection();
app.UseSwagger();
app.UseSwaggerUI();

// ── Endpoints ─────────────────────────────────────────────────────────────

app.MapProductEndpoints();
app.MapControllers();
app.MapOpenApi();

app.Run();
