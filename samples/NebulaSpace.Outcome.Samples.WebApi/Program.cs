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

// Rate limiting — fixed window, 10 req/min per client. On rejection the 429 body is the
// SAME ErrorResponse envelope as every other error, so .Produces<ErrorResponse>(429)
// documents what clients actually receive. HTTP status comes from the policy, the body
// from the app's error envelope.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        // Build the SAME ErrorResponse envelope as every other error, honoring the
        // cancellation token so a disconnecting client skips the serialization work.
        var resultOptions = context.HttpContext.RequestServices.GetRequiredService<ResultPatternOptions>();
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

// Result pattern options
builder.Services.AddOutcomeHttp(options =>
{
    // Success envelope — any type the app defines, serialized as-is, so
    // .Produces<SuccessResponse<T>>() documents the exact success shape per endpoint.
    // Pass the OPEN generic type; the library closes T per request. The type must
    // implement ISuccessEnvelope<T> so the library can build it (fail-fast at startup).
    options.SuccessEnvelope.Set(typeof(SuccessResponse<>));

    // The error envelope is any type the app defines — serialized as-is, so
    // .Produces<ErrorResponse>() documents the exact body. The type must implement
    // IErrorEnvelope so the library can build it (fail-fast at startup).
    options.ErrorEnvelope.Set(typeof(ErrorResponse));

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

// Rate limiter — BEFORE the exception handler and routing, so every endpoint is
// rate-limited and rejections produce the configured 429 envelope.
app.UseRateLimiter();

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