using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.Samples.WebApi.Failures;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Greeting;

public static class GreetingEndpoints
{
    public static IEndpointRouteBuilder MapGreetingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/greet")
            .MapGet("/{name}", (string name, IGreetingService greetingService, IHttpResultFactory http) =>
                greetingService.Greet(name).ToHttpResult(http, greeting => http.Ok(new { greeting = greeting ?? "" })))
            .Produces<SuccessResponse<GreetingResponse>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting("fixed");

        return app;
    }
}

/// <summary>Response payload for <c>GET /greet/{name}</c> — the <c>data</c> inside <see cref="SuccessResponse{T}"/>.</summary>
public sealed record GreetingResponse(string Greeting);
