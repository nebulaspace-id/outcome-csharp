using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// Thin adapter that bridges any <see cref="IResult"/> into MVC's
/// <see cref="IActionResult"/> pipeline. The existing envelope rendering, failure mapping,
/// and serialization logic in <see cref="ErrorEnvelopeResult"/>,
/// <see cref="SuccessEnvelopeResult{T}"/>, and <see cref="CreatedResult{T}"/> is reused
/// verbatim — zero duplication.
///
/// <para>
/// Works because MVC's <see cref="ActionContext"/> carries the same
/// <see cref="Microsoft.AspNetCore.Http.HttpContext"/> that <see cref="IResult.ExecuteAsync"/>
/// expects.
/// </para>
/// </summary>
public sealed class OutcomeActionResult : IActionResult
{
    private readonly IResult _inner;

    /// <summary>
    /// Wraps an <see cref="IResult"/> so it flows through MVC's filter pipeline.
    /// </summary>
    /// <param name="inner">The result produced by <see cref="IHttpResultFactory"/>.</param>
    public OutcomeActionResult(IResult inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>
    /// The wrapped <see cref="IResult"/>. Exposed for testability.
    /// </summary>
    public IResult Inner => _inner;

    /// <inheritdoc />
    public Task ExecuteResultAsync(ActionContext context) =>
        _inner.ExecuteAsync(context.HttpContext);
}
