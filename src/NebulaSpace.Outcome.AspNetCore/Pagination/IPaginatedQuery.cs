namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Marker interface for <c>[AsParameters]</c>-bound pagination query types.
///
/// <para>
/// The marker carries no members. The library's built-in query types clamp their values
/// to safe bounds — <see cref="DefaultOffsetQuery"/> caps page at
/// <see cref="DefaultOffsetQuery.MaxPage"/> and limit at
/// <see cref="DefaultOffsetQuery.MaxLimit"/>; <see cref="DefaultCursorQuery"/> caps limit
/// at <see cref="DefaultCursorQuery.MaxLimit"/>. A custom query type should apply its own
/// policy, including clamping every client-supplied value.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public sealed class MyQuery : IPaginatedQuery
/// {
///     private int _page = 1;
///     private int _limit = 10;
///
///     public int Page  { get => _page;  set => _page  = Math.Clamp(value, 1, 1_000); }
///     public int Limit { get => _limit; set => _limit = Math.Clamp(value, 1, 100); }
/// }
/// </code>
/// </example>
public interface IPaginatedQuery
{
}
