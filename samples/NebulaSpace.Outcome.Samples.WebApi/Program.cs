using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using NebulaSpace.Outcome.AspNetCore;
using NebulaSpace.Outcome.AspNetCore.Extensions;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.Samples.WebApi.Failures;
using NebulaSpace.Outcome.Samples.WebApi.Features.Greeting;
using NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddResultPattern(options =>
{
    options.EnvelopeSuccess = true;                // wrap success responses in SuccessResponse<T>

    // traceId is injected into error envelopes by default.
    // Add other properties to the error envelope:
    options.AddErrorEnvelopeProperty("environment", _ => builder.Environment.EnvironmentName);

    // Map application-defined failures to HTTP responses.
    // (Example — requires a QuotaExceededFailure type; remove if unused.)
    options.MapFailure<QuotaExceededFailure>(f =>
        Results.UnprocessableEntity(ErrorResponse.UnprocessableEntity(f.Message)));
});

// Services
builder.Services.AddSingleton<IGreetingService, GreetingService>();
builder.Services.AddSingleton<IOrderService, OrderService>();

var app = builder.Build();

app.UseHttpsRedirection();

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// Result pattern middleware (trace id injection)
app.UseResultPattern();

// Endpoints
app.MapGreetingEndpoints();
app.MapOrderEndpoints();
app.MapOpenApi();

app.Run();
