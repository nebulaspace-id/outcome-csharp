using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Incoming offset-pagination parameters using the bracketed query convention:
/// <c>?page[number]=1&amp;page[size]=10</c>.
///
/// <para>
/// This is a sample type, not part of the library. The library ships
/// <see cref="DefaultOffsetQuery"/>, which binds flat <c>page</c>/<c>limit</c> names.
/// Copy this class when an API needs the bracketed names; the service layer can stay
/// unchanged because the endpoint maps it to <see cref="OffsetQuery"/>.
/// </para>
///
/// <para>
/// Values are clamped to table-safe bounds: <see cref="Number"/> to
/// <c>1..MaxPage</c> and <see cref="Size"/> to <c>1..MaxSize</c>.
/// </para>
/// </summary>
public sealed class JsonApiOffsetQuery : IPaginatedQuery
{
    private const int MaxPage = 25;
    private const int MaxSize = 50;

    private int _number = 1;
    private int _size = 10;

    /// <summary>1-based page number, clamped to <c>1..MaxPage</c>.</summary>
    [FromQuery(Name = "page[number]")]
    public int Number
    {
        get => _number;
        set => _number = Math.Clamp(value, 1, MaxPage);
    }

    /// <summary>Items per page, clamped to <c>1..MaxSize</c>.</summary>
    [FromQuery(Name = "page[size]")]
    public int Size
    {
        get => _size;
        set => _size = Math.Clamp(value, 1, MaxSize);
    }

    /// <summary>
    /// The 0-based offset of the first item in this page, i.e. <c>(Number - 1) * Size</c>.
    /// Multiplied in 64-bit arithmetic so it cannot overflow.
    /// </summary>
    [BindNever]
    public int Offset => (int)Math.Min((long)(_number - 1) * _size, int.MaxValue);
}
