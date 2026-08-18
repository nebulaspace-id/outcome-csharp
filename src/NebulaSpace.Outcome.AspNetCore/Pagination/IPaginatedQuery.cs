using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore.Pagination;

/// <summary>
/// Marker interface for <c>[AsParameters]</c>-bound pagination query types.
/// Implement <see cref="Configure"/> to copy <see cref="PaginationOptions"/> into the
/// type's static cap fields — <c>AddOutcome</c> discovers every implementor and calls
/// <see cref="Configure"/> automatically, so adding a new query type never requires
/// touching the registration code.
///
/// <para>
/// The caps cannot be <c>static abstract</c> interface members because that would force
/// them to be <b>properties</b> — and ASP.NET Core's <c>RequestDelegateFactory</c> binds
/// <c>public static</c> properties on <c>[AsParameters]</c> types as query parameters,
/// emitting invalid IL (<see cref="System.InvalidProgramException"/>). Static
/// <b>fields</b> are invisible to the RDE, so <see cref="Configure"/> copies the values
/// into fields instead.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public sealed class MyQuery : IPaginatedQuery
/// {
///     public static int MaxPage = 25;   // field, not property — see remarks above
///     public static int MaxLimit = 50;
///
///     public static void Configure(PaginationOptions options)
///     {
///         MaxPage  = Math.Max(1, options.MaxPage);
///         MaxLimit = Math.Max(1, options.MaxLimit);
///     }
/// }
/// </code>
/// </example>
public interface IPaginatedQuery
{
    /// <summary>
    /// Copies the app's configured pagination caps into the type's static fields.
    /// Called once by <c>AddOutcome</c> after the user's configure callback runs.
    /// </summary>
    /// <param name="options">The pagination options from <see cref="OutcomeOptions.Pagination"/>.</param>
    static abstract void Configure(PaginationOptions options);
}
