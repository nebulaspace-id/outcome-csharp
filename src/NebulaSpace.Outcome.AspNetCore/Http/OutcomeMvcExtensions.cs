using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NebulaSpace.Outcome;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// Extension methods that convert an <see cref="Outcome{T}"/> directly to an
/// <see cref="IActionResult"/> through the supplied <see cref="IHttpResultFactory"/>,
/// without requiring an explicit <c>onSuccess</c> callback. The factory's default
/// mapping is used: success → <see cref="IHttpResultFactory.Ok{T}"/>,
/// failure → <see cref="IHttpResultFactory.FromFailure"/>.
/// </summary>
public static class OutcomeMvcExtensions
{
    /// <summary>
    /// Converts an <see cref="Outcome{T}"/> to an <see cref="IActionResult"/>:
    /// success produces <c>200 OK</c> with the configured envelope, failure maps through
    /// <see cref="IHttpResultFactory.FromFailure"/>.
    /// </summary>
    /// <param name="outcome">The outcome to convert.</param>
    /// <param name="factory">The <see cref="IHttpResultFactory"/> from DI.</param>
    public static IActionResult ToActionResult<T>(this Outcome<T> outcome, IHttpResultFactory factory)
        => new OutcomeActionResult(outcome.Match<IResult>(
            value => factory.Ok(value),
            failure => factory.FromFailure(failure)));

    /// <summary>
    /// Awaits a <see cref="Task{TResult}"/> of an <see cref="Outcome{T}"/> and converts
    /// it to an <see cref="IActionResult"/>.
    /// </summary>
    /// <param name="outcomeTask">The pending outcome.</param>
    /// <param name="factory">The <see cref="IHttpResultFactory"/> from DI.</param>
    public static async Task<IActionResult> ToActionResultAsync<T>(
        this Task<Outcome<T>> outcomeTask,
        IHttpResultFactory factory)
    {
        var outcome = await outcomeTask;
        return new OutcomeActionResult(outcome.Match<IResult>(
            value => factory.Ok(value),
            failure => factory.FromFailure(failure)));
    }

    /// <summary>
    /// Wraps any <see cref="IResult"/> (e.g. from <see cref="IHttpResultFactory"/>) as
    /// an <see cref="IActionResult"/> so it flows through MVC's filter pipeline.
    /// Use for non-outcome results like paginated responses built with the factory.
    /// </summary>
    /// <param name="result">The <see cref="IResult"/> to wrap.</param>
    public static IActionResult ToActionResult(this IResult result) =>
        new OutcomeActionResult(result);
}
