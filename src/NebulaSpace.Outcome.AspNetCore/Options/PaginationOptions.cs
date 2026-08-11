namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Pagination limits configurable at startup via <c>AddOutcome</c>. These values are copied
/// to static fields on <see cref="Pagination.OffsetQuery"/> and
/// <see cref="Pagination.CursorQuery"/> after <c>configure?.Invoke(options)</c>, so the
/// query types — bound as <c>[AsParameters]</c> with parameterless constructors — pick up
/// the app's chosen caps.
///
/// <para>
/// Defaults: <see cref="MaxPage"/> = 25, <see cref="MaxLimit"/> = 50. Both are clamped to
/// at least 1 by the consuming query types.
/// </para>
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddOutcome(options =>
/// {
///     options.Pagination.MaxPage = 50;
///     options.Pagination.MaxLimit = 100;
/// });
/// </code>
/// </example>
public sealed class PaginationOptions
{
    /// <summary>
    /// Maximum page number a client can request for offset pagination (<see cref="Pagination.OffsetQuery"/>).
    /// Default 25. Clamped to at least 1.
    /// </summary>
    public int MaxPage { get; set; } = 25;

    /// <summary>
    /// Maximum items per page (limit) a client can request for both offset
    /// (<see cref="Pagination.OffsetQuery"/>) and cursor (<see cref="Pagination.CursorQuery"/>)
    /// pagination. Default 50. Clamped to at least 1.
    /// </summary>
    public int MaxLimit { get; set; } = 50;
}
