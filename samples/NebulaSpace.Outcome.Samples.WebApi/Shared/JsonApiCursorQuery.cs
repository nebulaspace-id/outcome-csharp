using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
/// <see cref="Size"/> is nullable so an omitted key falls back to a default instead of
/// failing model binding. <see cref="Limit"/> clamps it to <c>1..MaxSize</c> so a hostile
/// query cannot request an unbounded page.
/// </para>
/// </summary>
public sealed class JsonApiCursorQuery : IPaginatedQuery
{
    private const int MaxSize = 50;
    private const int DefaultSize = 10;

    /// <summary>
    /// Opaque cursor pointing to the start of the next page. <c>null</c> for the first
    /// page.
    /// </summary>
    [FromQuery(Name = "page[cursor]")]
    public string? Cursor { get; set; }

    /// <summary>Requested items per page; <c>null</c> when the client omitted it.</summary>
    [FromQuery(Name = "page[size]")]
    public int? Size { get; set; }

    /// <summary>Effective items per page, clamped to <c>1..MaxSize</c>.</summary>
    [BindNever]
    public int Limit => Math.Clamp(Size ?? DefaultSize, 1, MaxSize);
}
