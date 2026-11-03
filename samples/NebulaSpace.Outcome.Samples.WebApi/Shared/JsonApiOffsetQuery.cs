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
/// The bound properties are nullable so an omitted key falls back to a default instead of
/// failing model binding. <see cref="Page"/> and <see cref="Limit"/> clamp the result:
/// page to <c>1..MaxPage</c>, size to <c>1..MaxSize</c>.
/// </para>
/// </summary>
public sealed class JsonApiOffsetQuery : IPaginatedQuery
{
    private const int MaxPage = 25;
    private const int MaxSize = 50;
    private const int DefaultNumber = 1;
    private const int DefaultSize = 10;

    /// <summary>Requested page number; <c>null</c> when the client omitted it.</summary>
    [FromQuery(Name = "page[number]")]
    public int? Number { get; set; }

    /// <summary>Requested items per page; <c>null</c> when the client omitted it.</summary>
    [FromQuery(Name = "page[size]")]
    public int? Size { get; set; }

    /// <summary>Effective 1-based page number, clamped to <c>1..MaxPage</c>.</summary>
    [BindNever]
    public int Page => Math.Clamp(Number ?? DefaultNumber, 1, MaxPage);

    /// <summary>Effective items per page, clamped to <c>1..MaxSize</c>.</summary>
    [BindNever]
    public int Limit => Math.Clamp(Size ?? DefaultSize, 1, MaxSize);

    /// <summary>
    /// The 0-based offset of the first item in this page, i.e. <c>(Page - 1) * Limit</c>.
    /// Multiplied in 64-bit arithmetic so it cannot overflow.
    /// </summary>
    [BindNever]
    public int Offset => (int)Math.Min((long)(Page - 1) * Limit, int.MaxValue);
}
