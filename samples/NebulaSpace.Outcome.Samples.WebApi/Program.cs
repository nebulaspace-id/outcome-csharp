using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using NebulaSpace.Outcome.AspNetCore.Extensions;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.Samples.WebApi.Failures;
using NebulaSpace.Outcome.Samples.WebApi.Features.Exceptions;
using NebulaSpace.Outcome.Samples.WebApi.Features.Greeting;
using NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

var builder = WebApplication.CreateBuilder(args);

// JSON options — affects Results.* and WriteAsJsonAsync (including the library's error
// envelopes). Best practice: omit nulls, camelCase, strict numbers, enums as strings.
// Minimal API:
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNameCaseInsensitive = false;
    options.SerializerOptions.AllowDuplicateProperties = false;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// MVC (controllers) — configured separately from Minimal API:
builder.Services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
    options.JsonSerializerOptions.AllowDuplicateProperties = false;
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Result Samples API",
        Version = "v1",
        Description = "Example API using the Result pattern.",
    });
});

// Result pattern options
builder.Services.AddOutcomeHttp(options =>
{
    options.SuccessEnvelope.Enabled = true; // wrap success responses in SuccessResponse<T>

    // The default error shape is clean RFC 7807 ProblemDetails: only title/status/detail.
    // Envelope properties (appear ONLY on error responses) are opt-in:
    options.ErrorEnvelope.Add("traceId", context => context.TraceIdentifier);
    options.ErrorEnvelope.Add("environment", _ => builder.Environment.EnvironmentName);

    // Map application-defined failures to HTTP responses. The factory returns a
    // concrete IErrorResponse (ProblemDetails or ErrorResponse); envelope properties
    // still inject on top of it.
    // (Example — requires a QuotaExceededFailure type; remove if unused.)
    options.Failures.Map<QuotaExceededFailure>(f =>
        ErrorResponse.UnprocessableEntity(f.Message, f.Code));
});

// Unhandled exceptions → the same error envelope (status 500) as expected failures.
// Register first, then UseOutcomeExceptionHandler() in the pipeline.
builder.Services.AddOutcomeExceptionHandler(options =>
{
    // Map known exception types to a specific envelope — bypasses the generic 500
    // fallback (and IncludeExceptionDetails). E.g. a domain exception becomes 422;
    // the error code comes from the exception instance, not a hardcoded string:
    options.Exceptions.Map<DomainException>(ex =>
        ErrorResponse.UnprocessableEntity(ex.Message, ex.Code));

    // FluentValidation failures → 400 Bad Request with the field errors. Multiple messages
    // for the same property are joined with "; ". The errors come from the
    // ValidationException instance (property name → message), not hardcoded.
    options.Exceptions.Map<ValidationException>(ex =>
        ErrorResponse.BadRequest("Validation Error",
            errors: ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => string.Join("; ", g.Select(e => e.ErrorMessage)))));

    // Only in Development should UNHANDLED exception details leak into the response body.
    options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
});

// Services
builder.Services.AddSingleton<IGreetingService, GreetingService>();
builder.Services.AddSingleton<IOrderService, OrderService>();
builder.Services.AddSingleton<IExceptionDemoService, ExceptionDemoService>();

// FluentValidation validators
builder.Services.AddSingleton<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();

var app = builder.Build();

// Exception handler middleware — EARLY, before routing, so unhandled exceptions from
// any downstream middleware or endpoint produce the same error envelope as expected
// failures.
app.UseOutcomeExceptionHandler();

app.UseHttpsRedirection();

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// Endpoints
app.MapGreetingEndpoints();
app.MapOrderEndpoints();
app.MapExceptionDemoEndpoints();
app.MapOpenApi();

app.Run();