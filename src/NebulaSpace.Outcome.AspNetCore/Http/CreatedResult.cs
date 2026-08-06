using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// A 201 Created result that derives its <c>Location</c> header from the current
/// request path plus the new entity's id, so callers never write the uri by hand.
/// </summary>
public sealed class CreatedResult<T> : IResult
{
    private readonly T _data;
    private readonly Func<T, object> _idSelector;
    private readonly ResultPatternOptions _options;

    public CreatedResult(T data, Func<T, object> idSelector, ResultPatternOptions options)
    {
        _data = data;
        _idSelector = idSelector;
        _options = options;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var id = _idSelector(_data);
        var path = httpContext.Request.Path.ToString().TrimEnd('/');

        object body = _options.SuccessEnvelope.Enabled
            ? SuccessResponse<T>.Created(_data)
            : _data!;

        httpContext.Response.StatusCode = StatusCodes.Status201Created;
        httpContext.Response.Headers.Location = $"{path}/{id}";
        await httpContext.Response.WriteAsJsonAsync(body);
    }
}
