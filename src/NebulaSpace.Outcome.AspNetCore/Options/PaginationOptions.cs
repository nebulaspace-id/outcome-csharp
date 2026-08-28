using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Pagination limits configurable at startup via <c>AddOutcome</c>. These values are copied
/// to static fields on <see cref="Pagination.DefaultOffsetQuery"/> and
/// <see cref="Pagination.DefaultCursorQuery"/> after <c>configure?.Invoke(options)</c>, so the
/// query types — bound as <c>[AsParameters]</c> with parameterless constructors — pick up
/// the app's chosen caps.
///
/// <para>
/// Defaults: <see cref="MaxPage"/> = <see cref="int.MaxValue"/> (uncapped) and
/// <see cref="MaxLimit"/> = <see cref="int.MaxValue"/> (uncapped). Set them to apply caps.
/// Both are clamped to at least 1 by the consuming query types.
/// </para>
///
/// <para>
/// Query types are configured either by assembly scanning (default, reflection-based) or,
/// for NativeAOT, by explicit registration — see <see cref="ScanAssemblies"/> and
/// <see cref="RegisterQuery{TQuery}"/>.
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

    /// <summary>
    /// Whether <c>AddOutcome</c> scans loaded assemblies for <c>IPaginatedQuery</c>
    /// implementors and calls their <c>Configure</c> via reflection. Default <c>true</c> —
    /// custom query types are picked up automatically, no wiring needed.
    ///
    /// <para>
    /// For NativeAOT, set this to <c>false</c> and register each query type explicitly with
    /// <see cref="RegisterQuery{TQuery}"/>. That path calls <c>Configure</c> through static
    /// interface dispatch — no reflection.
    /// </para>
    /// </summary>
    public bool ScanAssemblies { get; set; } = true;

    private readonly List<Action<PaginationOptions>> _queryConfigurators = new();

    /// <summary>
    /// Registers a pagination query type so <c>AddOutcome</c> calls its <c>Configure</c>
    /// through static interface dispatch — no reflection. The AOT-safe alternative to
    /// assembly scanning (see <see cref="ScanAssemblies"/>).
    ///
    /// <para>
    /// Use it for your custom query types and, when scanning is disabled, for the built-in
    /// <c>DefaultOffsetQuery</c> / <c>DefaultCursorQuery</c>.
    /// </para>
    /// </summary>
    /// <typeparam name="TQuery">A class implementing <c>IPaginatedQuery</c>.</typeparam>
    /// <example>
    /// <code>
    /// options.Pagination.ScanAssemblies = false;
    /// options.Pagination.RegisterQuery&lt;MyQuery&gt;();
    /// options.Pagination.RegisterQuery&lt;DefaultOffsetQuery&gt;();
    /// options.Pagination.RegisterQuery&lt;DefaultCursorQuery&gt;();
    /// </code>
    /// </example>
    public void RegisterQuery<TQuery>() where TQuery : class, IPaginatedQuery
        => _queryConfigurators.Add(options => TQuery.Configure(options));

    /// <summary>
    /// Explicitly registered query configurators (AOT-safe path). Run by <c>AddOutcome</c>
    /// after the optional assembly scan, for every query type registered via
    /// <see cref="RegisterQuery{TQuery}"/>.
    /// </summary>
    internal IReadOnlyList<Action<PaginationOptions>> QueryConfigurators => _queryConfigurators;
}
