using Microsoft.AspNetCore.Mvc;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Incoming cursor-pagination parameters using the bracketed query convention:
/// <c>?page[cursor]=...&amp;page[size]=10</c>.
///
/// <para>
/// This is a sample type, not part of the library. The library ships
/// <see cref="DefaultCursorQuery"/>, which binds flat <c>cursor</c>/<c>limit</c> names.
/// Copy this class when an API needs the bracketed names; the service layer can stay
/// unchanged because the endpoint maps it to <see cref="CursorQuery"/>.
/// </para>
///
/// <para>
/// <see cref="Size"/> is clamped to <c>1..MaxSize</c> so a hostile query cannot request
/// an unbounded page. The cursor stays opaque — the sample treats it as an untrusted
/// string and the service decides whether it is valid.
/// </para>
/// </summary>
public sealed class JsonApiCursorQuery : IPaginatedQuery
{
    private const int MaxSize = 50;

    private string? _cursor;
    private int _size = 10;

    /// <summary>
    /// Opaque cursor pointing to the start of the next page. <c>null</c> for the first
    /// page.
    /// </summary>
    [FromQuery(Name = "page[cursor]")]
    public string? Cursor
    {
        get => _cursor;
        set => _cursor = value;
    }

    /// <summary>Items per page, clamped to <c>1..MaxSize</c>.</summary>
    [FromQuery(Name = "page[size]")]
    public int Size
    {
        get => _size;
        set => _size = Math.Clamp(value, 1, MaxSize);
    }
}
