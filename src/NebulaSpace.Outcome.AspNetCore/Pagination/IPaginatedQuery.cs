namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Marker interface for <c>[AsParameters]</c>-bound pagination query types.
///
/// <para>
/// The marker carries no members and enforces no limits — the library
/// ships no pagination caps. <c>Page</c>/<c>Limit</c> on the built-in
/// queries are clamped to a minimum of 1 only. If you need maximum
/// bounds, implement them in your own query type.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public sealed class MyQuery : IPaginatedQuery
/// {
///     private int _page = 1;
///     private int _limit = 10;
///
///     public int Page  { get => _page;  set => _page  = Math.Max(1, value); }
///     public int Limit { get => _limit; set => _limit = Math.Max(1, value); }
/// }
/// </code>
/// </example>
public interface IPaginatedQuery
{
}
