using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.Samples.WebApi.Failures;
using NebulaSpace.Outcome.Samples.WebApi.Features.Greeting;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Greeting;

public static class GreetingEndpoints
{
    public static IEndpointRouteBuilder MapGreetingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/greet")
            .MapGet("/{name}", (string name, IGreetingService greetingService, IHttpResultFactory http) =>
                greetingService.Greet(name).ToHttpResult(http, greeting => http.Ok(new { greeting = greeting ?? "" })))
            .Produces<DefaultSuccessResponse<GreetingResponse>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}

/// <summary>Response payload for <c>GET /greet/{name}</c> — the <c>data</c> inside <see cref="DefaultSuccessResponse{T}"/>.</summary>
public sealed record GreetingResponse(string Greeting);
