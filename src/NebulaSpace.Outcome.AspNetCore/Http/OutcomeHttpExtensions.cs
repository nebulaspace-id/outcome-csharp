using Microsoft.AspNetCore.Http;
using NebulaSpace.Outcome;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// Extension methods bridging <see cref="Outcome{T}"/> and <see cref="Failure"/> to
/// ASP.NET Core's <see cref="IResult"/>, so endpoints never write the failure branch by hand.
/// </summary>
public static class OutcomeHttpExtensions
{
    /// <summary>
    /// Maps a <see cref="Failure"/> to its HTTP error response through the supplied
    /// <see cref="IHttpResultFactory"/>.
    /// </summary>
    public static IResult ToErrorResponse(this Failure failure, IHttpResultFactory factory)
        => factory.FromFailure(failure);

    /// <summary>
    /// Converts an <see cref="Outcome{T}"/> to an HTTP response: <paramref name="onSuccess"/>
    /// builds the success result, and a failure becomes its error response.
    /// </summary>
    public static IResult ToHttpResult<T>(this Outcome<T> outcome, IHttpResultFactory factory, Func<T, IResult> onSuccess)
        => outcome.Match(onSuccess, failure => failure.ToErrorResponse(factory));

    /// <summary>
    /// Awaits a <see cref="Task{TResult}"/> of an <see cref="Outcome{T}"/> and converts it
    /// to an HTTP response. Use with async services that return <c>Task&lt;Outcome&lt;T&gt;&gt;</c>.
    /// </summary>
    public static async Task<IResult> ToHttpResultAsync<T>(
        this Task<Outcome<T>> outcomeTask,
        IHttpResultFactory factory,
        Func<T, IResult> onSuccess)
    {
        var outcome = await outcomeTask;
        return outcome.Match(onSuccess, failure => failure.ToErrorResponse(factory));
    }

    /// <summary>
    /// Awaits a <see cref="Task{TResult}"/> of an <see cref="Outcome{T}"/> and converts it
    /// to an HTTP response, with an asynchronous success handler (e.g. one that awaits
    /// a downstream service).
    /// </summary>
    public static async Task<IResult> ToHttpResultAsync<T>(
        this Task<Outcome<T>> outcomeTask,
        IHttpResultFactory factory,
        Func<T, Task<IResult>> onSuccess)
    {
        var outcome = await outcomeTask;
        if (outcome.IsSuccess)
        {
            return await onSuccess(outcome.Value);
        }

        return outcome.Failure.ToErrorResponse(factory);
    }
}
