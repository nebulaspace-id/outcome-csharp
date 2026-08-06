using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.Samples.WebApi.Features.Greeting;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Greeting;

public static class GreetingEndpoints
{
    public static IEndpointRouteBuilder MapGreetingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/greet")
            .MapGet("/{name}", (string name, IGreetingService greetingService, IHttpResultFactory http) =>
                greetingService.Greet(name).ToHttpResult(http, greeting => http.Ok(new { greeting = greeting ?? "" })));

        return app;
    }
}
