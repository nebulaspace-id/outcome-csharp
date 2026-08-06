using NebulaSpace.Outcome.AspNetCore.Http;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Exceptions;

public static class ExceptionDemoEndpoints
{
    public static IEndpointRouteBuilder MapExceptionDemoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/demo/exceptions")
            .MapGet("/{keyword}", (string keyword, IExceptionDemoService service, IHttpResultFactory http) =>
            {
                // 'reserved' → throws DomainException inside the service; the middleware
                // maps it to 422 Unprocessable Entity via options.Exceptions.Map.
                return http.Ok(new { message = service.Reserve(keyword) });
            });

        return app;
    }
}
