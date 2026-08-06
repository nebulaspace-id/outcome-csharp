using Microsoft.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Envelope for a successful operation. Carries the payload and optional pagination.
/// </summary>
public sealed class SuccessResponse<T> : ResponseBody
{
    public T? Data { get; set; }
    public Pagination.Pagination? Pagination { get; set; }

    public static SuccessResponse<T> Ok(T? data, Pagination.Pagination? pagination = null) => new()
    {
        StatusCode = StatusCodes.Status200OK,
        Data = data,
        Pagination = pagination,
    };

    internal static SuccessResponse<T> Created(T data) => new()
    {
        StatusCode = StatusCodes.Status201Created,
        Data = data,
    };
}
