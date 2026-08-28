namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Pagination limits configurable at startup via <c>AddOutcome</c>. These values are copied
/// to static fields on <see cref="Pagination.DefaultOffsetQuery"/> and
/// <see cref="Pagination.DefaultCursorQuery"/> after <c>configure?.Invoke(options)</c>, so the
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
    /// Maximum page number a client can request for offset pagination (<see cref="Pagination.DefaultOffsetQuery"/>).
    /// Default <see cref="int.MaxValue"/> — uncapped unless the app sets a value.
    /// Clamped to at least 1 by the consuming query types.
    /// </summary>
    public int MaxPage { get; set; } = int.MaxValue;

    /// <summary>
    /// Maximum items per page (limit) a client can request for both offset
    /// (<see cref="Pagination.DefaultOffsetQuery"/>) and cursor (<see cref="Pagination.DefaultCursorQuery"/>)
    /// pagination. Default <see cref="int.MaxValue"/> — uncapped unless the app sets a value.
    /// Clamped to at least 1 by the consuming query types.
    /// </summary>
    public int MaxLimit { get; set; } = int.MaxValue;
}
