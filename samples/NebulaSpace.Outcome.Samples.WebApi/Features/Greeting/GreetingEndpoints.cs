using NebulaSpace.Outcome.AspNetCore;
using NebulaSpace.Outcome.AspNetCore.Http;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Greeting;

public static class GreetingEndpoints
{
    public static IEndpointRouteBuilder MapGreetingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/greet")
            .MapGet("/{name}", (string name, IGreetingService greetingService) =>
                greetingService.Greet(name).ToHttpResult(greeting => HttpResult.Ok(new { greeting = greeting ?? "" })));

        return app;
    }
}