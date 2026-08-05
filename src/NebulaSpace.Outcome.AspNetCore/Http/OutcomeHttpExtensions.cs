using Microsoft.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// Extension methods bridging <see cref="Outcome{T}"/> and <see cref="Failure"/> to
/// ASP.NET Core's <see cref="IResult"/>, so endpoints never write the failure branch by hand.
/// </summary>
public static class OutcomeHttpExtensions
{
    /// <summary>
    /// Maps a <see cref="Failure"/> to its HTTP error response, honoring failures
    /// registered via <c>MapFailure</c> and the built-in failure types.
    /// </summary>
    public static IResult ToErrorResponse(this Failure failure)
        => HttpResult.FromFailure(failure);

    /// <summary>
    /// Converts an <see cref="Outcome{T}"/> to an HTTP response: <paramref name="onSuccess"/>
    /// builds the success result, and a failure becomes its error response.
    /// </summary>
    public static IResult ToHttpResult<T>(this Outcome<T> outcome, Func<T, IResult> onSuccess)
        => outcome.Match(onSuccess, failure => failure.ToErrorResponse());
}
