using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Models;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// A successful <see cref="IResult"/> that wraps the payload in the configured success
/// envelope — or returns it raw when <see cref="SuccessOptions.Enabled"/> is
/// <c>false</c>. The envelope is built at execution time so the factory can read the
/// current <see cref="HttpContext"/> (for <c>traceId</c>, <c>path</c>, etc.) and the
/// response's status code.
///
/// <para>
/// Resolution order when envelopes are enabled:
/// <list type="number">
/// <item>Pagination + custom 2‑param envelope → <c>SuccessPaginationFactory</c></item>
/// <item>Pagination + no 2‑param envelope → <see cref="DefaultSuccessResponse{TData,TPage}"/> (auto‑fallback)</item>
/// <item>No pagination + custom 1‑param envelope → <c>SuccessFactory</c></item>
/// <item>No pagination + no envelope → <see cref="DefaultSuccessResponse{T}"/></item>
/// </list>
/// Pagination metadata is never silently dropped.
/// </para>
/// </summary>
public sealed class SuccessEnvelopeResult<T> : IResult
{
    private readonly T? _data;
    private readonly IPaginationDetails? _pagination;
    private readonly int _statusCode;
    private readonly OutcomeOptions _options;

    public SuccessEnvelopeResult(T? data, IPaginationDetails? pagination, int statusCode, OutcomeOptions options)
    {
        _data = data;
        _pagination = pagination;
        _statusCode = statusCode;
        _options = options;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        // When the envelope is disabled, return the raw payload. If the payload is null,
        // return an empty object so WriteAsJsonAsync produces "{}" instead of "null" —
        // a null body that says the request was successful is misleading.
        object body = !_options.Success.Enabled
            ? (object?)_data ?? new {}
            : _pagination is not null
                ? _options.SuccessPaginationFactory is { } paginationFactory
                    ? paginationFactory(_data, _pagination, httpContext, _statusCode)
                    : BuildDefaultPaginated(_data, _pagination, httpContext)
                : _options.SuccessFactory is { } factory
                    ? factory(_data, _pagination, httpContext, _statusCode)
                    : (object)BuildDefault(_data, _pagination, httpContext);

        httpContext.Response.StatusCode = _statusCode;
        // Cast to object so System.Text.Json serializes the runtime type's full shape.
        await httpContext.Response.WriteAsJsonAsync(body);
    }

    /// <summary>
    /// Auto‑fallback for paginated responses when the user has not registered a 2‑param
    /// envelope — resolves <c>DefaultSuccessResponse&lt;TData, TPage&gt;</c> so pagination
    /// metadata is never silently dropped.
    /// </summary>
    private object BuildDefaultPaginated(T? data, IPaginationDetails pagination, HttpContext httpContext)
    {
        var dataType = data?.GetType() ?? typeof(T);
        var paginationType = pagination.GetType();
        var targetType = typeof(DefaultSuccessResponse<,>).MakeGenericType(dataType, paginationType);

        var fromMethod = DefaultPaginatedFromCache.GetOrAdd(targetType, t =>
            t.GetMethod("From", BindingFlags.Public | BindingFlags.Static)!);

        return fromMethod.Invoke(null, new object?[] { data, pagination, httpContext, _statusCode })!;
    }

    /// <summary>
    /// Per‑closed‑type <c>MethodInfo</c> cache for the paginated fallback path.
    /// One entry per distinct (TData, TPage) combination across the process lifetime.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, MethodInfo> DefaultPaginatedFromCache = new();

    /// <summary>
    /// Builds the default non‑paginated envelope via its <c>From</c> factory, matching
    /// its status code to the response.
    /// </summary>
    private DefaultSuccessResponse<T> BuildDefault(T? data, IPaginationDetails? pagination, HttpContext httpContext) =>
        (DefaultSuccessResponse<T>)DefaultSuccessResponse<T>.From(data!, pagination, httpContext, _statusCode);
}
