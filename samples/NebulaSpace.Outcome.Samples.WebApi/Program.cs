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
    options.SuccessEnvelope.Enabled = true; // wrap success responses in DefaultSuccessResponse<T>

    // The error envelope is any type the app defines — serialized as-is, so
    // .Produces<ErrorResponse>() documents the exact body.
    options.ErrorEnvelope.Set(ErrorResponse.From);

    // Map application-defined failures to HTTP intent (FailureDetails) — the one
    // ErrorResponse shape renders EVERY error, including mapped failures.
    options.Failures.Map<QuotaExceededFailure>(f =>
        new FailureDetails(StatusCodes.Status422UnprocessableEntity, f.Message, f.Code));
});

// Unhandled exceptions → the same error envelope (status 500) as expected failures.
// Register first, then UseOutcomeExceptionHandler() in the pipeline.
builder.Services.AddOutcomeExceptionHandler(options =>
{
    // Exception mappers return HTTP INTENT (FailureDetails), not an envelope — the one
    // ErrorResponse shape renders every exception too. E.g. a domain exception becomes 422:
    options.Exceptions.Map<DomainException>(ex =>
        new FailureDetails(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            message: ex.Message,
            code: "domain_error")
    );

    // FluentValidation failures → 400 Bad Request with field errors. Per RFC 9457 each
    // field maps to an ARRAY of messages — one message is a single-element array, several
    // become a longer array. Errors come from the instance.
    options.Exceptions.Map<ValidationException>(ex =>
        new FailureDetails(
            statusCode: StatusCodes.Status400BadRequest,
            message: "Validation Error",
            code: "validation_error",
            errors: ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));

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