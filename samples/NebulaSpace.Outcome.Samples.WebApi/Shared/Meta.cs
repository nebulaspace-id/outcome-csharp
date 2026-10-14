using Microsoft.AspNetCore.Http;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// The app's response <b>metadata</b> — one type for the whole API, not one per
/// endpoint. It carries whatever the API wants clients to see alongside the payload:
/// pagination, timestamps, trace ids, links, and anything added later.
///
/// <para>
/// Build it with <see cref="MetaBuilder"/> in the <b>endpoint layer</b>; the library
/// only carries it through to the envelope and never inspects it.
/// </para>
///
/// <para>
/// <see cref="Pagination"/> is typed <see cref="object"/> because not every response is
/// paged. System.Text.Json serializes an object-typed property by its runtime type, so a
/// paginated response still emits the full pagination shape (<c>page</c>, <c>limit</c>,
/// <c>totalPages</c>, …) rather than an empty interface contract. When a response is not
/// paged the property stays <c>null</c>, and with
/// <c>JsonIgnoreCondition.WhenWritingNull</c> the key is omitted entirely.
/// </para>
/// </summary>
public sealed class Meta
{
    /// <summary>Pagination for this page, when the response is paged; else <c>null</c>.</summary>
    public object? Pagination { get; }

    /// <summary>When this response was produced (UTC).</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>The request's trace id — <c>HttpContext.TraceIdentifier</c>.</summary>
    public string? TraceId { get; }

    internal Meta(object? pagination, DateTimeOffset timestamp, string? traceId)
    {
        Pagination = pagination;
        Timestamp = timestamp;
        TraceId = traceId;
    }

    /// <summary>
    /// Starts building metadata for the current request — fills the per-request
    /// fields (timestamp, trace id) automatically.
    /// </summary>
    public static MetaBuilder Builder(HttpContext httpContext) => new(httpContext);
}

/// <summary>
/// Fluent builder for <see cref="Meta"/>. Start with <see cref="Meta.Builder"/>,
/// add what the response carries, then <see cref="Build"/>.
///
/// <code>
/// http.Ok(page.Items, Meta.Builder(context)
///     .SetPagination(page.ToOffsetPagination(query))
///     .Build())
/// </code>
///
/// <para>
/// Add a <c>Set…</c> method here when the API needs another metadata field; endpoints
/// keep reading as one fluent line.
/// </para>
/// </summary>
public sealed class MetaBuilder
{
    private readonly HttpContext _httpContext;
    private object? _pagination;

    internal MetaBuilder(HttpContext httpContext) => _httpContext = httpContext;

    /// <summary>
    /// Attaches the pagination for this page. The value is carried as-is and
    /// serialized by its runtime type, so its full shape reaches the client.
    /// </summary>
    public MetaBuilder SetPagination(object pagination)
    {
        _pagination = pagination;
        return this;
    }

    /// <summary>Builds the immutable <see cref="Meta"/> for this response.</summary>
    public Meta Build() => new(_pagination, DateTimeOffset.UtcNow, _httpContext.TraceIdentifier);
}
