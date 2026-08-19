using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NebulaSpace.Outcome;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// Extension methods bridging <see cref="Outcome{T}"/> and <see cref="Failure"/> to
/// ASP.NET Core's <see cref="IResult"/>, so endpoints never write the failure branch
/// by hand.
/// </summary>
public static class OutcomeHttpExtensions
{
    /// <summary>
    /// Maps a <see cref="Failure"/> to its HTTP error response through the supplied
    /// <see cref="IHttpResultFactory"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// IResult error = failure.ToErrorResponse(http);
    /// </code>
    /// </example>
    public static IResult ToErrorResponse(this Failure failure, IHttpResultFactory factory)
        => factory.FromFailure(failure);

    /// <summary>
    /// Converts an <see cref="Outcome{T}"/> to an HTTP response:
    /// <paramref name="onSuccess"/> builds the success result, and a failure becomes
    /// its error response automatically.
    /// </summary>
    /// <param name="outcome">The outcome to convert.</param>
    /// <param name="factory">The <see cref="IHttpResultFactory"/> from DI.</param>
    /// <param name="onSuccess">Builds the success <see cref="IResult"/> from the payload.</param>
    /// <example>
    /// <code>
    /// IResult result = outcome.ToResult(http, value => http.Ok(new { value }));
    /// </code>
    /// </example>
    public static IResult ToResult<T>(this Outcome<T> outcome, IHttpResultFactory factory, Func<T, IResult> onSuccess)
        => outcome.Match(onSuccess, failure => failure.ToErrorResponse(factory));

    /// <summary>
    /// Awaits a <see cref="Task{TResult}"/> of an <see cref="Outcome{T}"/> and converts
    /// it to an HTTP response. Use with async services that return
    /// <c>Task&lt;Outcome&lt;T&gt;&gt;</c>.
    /// </summary>
    /// <param name="outcomeTask">The pending outcome.</param>
    /// <param name="factory">The <see cref="IHttpResultFactory"/> from DI.</param>
    /// <param name="onSuccess">Builds the success <see cref="IResult"/> from the payload.</param>
    /// <example>
    /// <code>
    /// IResult result = await outcomeTask.ToResultAsync(http, value => http.Ok(value));
    /// </code>
    /// </example>
    public static async Task<IResult> ToResultAsync<T>(
        this Task<Outcome<T>> outcomeTask,
        IHttpResultFactory factory,
        Func<T, IResult> onSuccess)
    {
        var outcome = await outcomeTask;
        return outcome.Match(onSuccess, failure => failure.ToErrorResponse(factory));
    }

    /// <summary>
    /// Awaits a <see cref="Task{TResult}"/> of an <see cref="Outcome{T}"/> and converts
    /// it to an HTTP response, with an <b>asynchronous</b> success handler (e.g. one that
    /// awaits a downstream service or write).
    /// </summary>
    /// <param name="outcomeTask">The pending outcome.</param>
    /// <param name="factory">The <see cref="IHttpResultFactory"/> from DI.</param>
    /// <param name="onSuccess">An async function that builds the success <see cref="IResult"/>.</param>
    /// <example>
    /// <code>
    /// IResult result = await outcomeTask.ToResultAsync(
    ///     http, async order => await store.Save(order, http));
    /// </code>
    /// </example>
    public static async Task<IResult> ToResultAsync<T>(
        this Task<Outcome<T>> outcomeTask,
        IHttpResultFactory factory,
        Func<T, Task<IResult>> onSuccess)
    {
        var outcome = await outcomeTask;
        return await outcome.Match(
            onSuccess: v => onSuccess(v),
            onFailure: f => Task.FromResult(f.ToErrorResponse(factory)));
    }

    // ──────────────────────────────────────────────────────────────────────
    //  MVC bridge — same semantics, returns IActionResult for controller use
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts an <see cref="Outcome{T}"/> to an <see cref="IActionResult"/>:
    /// <paramref name="onSuccess"/> builds the success result, and a failure becomes
    /// its error response automatically. The result flows through MVC's filter pipeline.
    /// </summary>
    /// <param name="outcome">The outcome to convert.</param>
    /// <param name="factory">The <see cref="IHttpResultFactory"/> from DI.</param>
    /// <param name="onSuccess">Builds the success <see cref="IResult"/> from the payload.</param>
    /// <example>
    /// <code>
    /// IActionResult result = outcome.ToActionResult(http, value => http.Ok(value));
    /// </code>
    /// </example>
    public static IActionResult ToActionResult<T>(this Outcome<T> outcome, IHttpResultFactory factory, Func<T, IResult> onSuccess)
        => new OutcomeActionResult(outcome.ToResult(factory, onSuccess));

    /// <summary>
    /// Awaits a <see cref="Task{TResult}"/> of an <see cref="Outcome{T}"/> and converts
    /// it to an <see cref="IActionResult"/>. Use with async services that return
    /// <c>Task&lt;Outcome&lt;T&gt;&gt;</c>.
    /// </summary>
    /// <param name="outcomeTask">The pending outcome.</param>
    /// <param name="factory">The <see cref="IHttpResultFactory"/> from DI.</param>
    /// <param name="onSuccess">Builds the success <see cref="IResult"/> from the payload.</param>
    /// <example>
    /// <code>
    /// IActionResult result = await outcomeTask.ToActionResultAsync(http, value => http.Ok(value));
    /// </code>
    /// </example>
    public static async Task<IActionResult> ToActionResultAsync<T>(
        this Task<Outcome<T>> outcomeTask,
        IHttpResultFactory factory,
        Func<T, IResult> onSuccess)
    {
        var inner = await outcomeTask.ToResultAsync(factory, onSuccess);
        return new OutcomeActionResult(inner);
    }

    /// <summary>
    /// Awaits a <see cref="Task{TResult}"/> of an <see cref="Outcome{T}"/> and converts
    /// it to an <see cref="IActionResult"/>, with an <b>asynchronous</b> success handler.
    /// </summary>
    /// <param name="outcomeTask">The pending outcome.</param>
    /// <param name="factory">The <see cref="IHttpResultFactory"/> from DI.</param>
    /// <param name="onSuccess">An async function that builds the success <see cref="IResult"/>.</param>
    /// <example>
    /// <code>
    /// IActionResult result = await outcomeTask.ToActionResultAsync(
    ///     http, async order => await store.Save(order, http));
    /// </code>
    /// </example>
    public static async Task<IActionResult> ToActionResultAsync<T>(
        this Task<Outcome<T>> outcomeTask,
        IHttpResultFactory factory,
        Func<T, Task<IResult>> onSuccess)
    {
        var inner = await outcomeTask.ToResultAsync(factory, onSuccess);
        return new OutcomeActionResult(inner);
    }
}
