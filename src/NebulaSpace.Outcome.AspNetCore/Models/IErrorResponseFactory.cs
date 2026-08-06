using NebulaSpace.Outcome.AspNetCore.Http;

namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Strategy that decides the error envelope shape. Registered in DI and consumed by
/// <see cref="HttpResultFactory"/>. Replace it (e.g. with a
/// <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/>-based implementation) to change
/// the shape of every error response the library produces without touching the library.
/// Per-failure overrides are handled separately via <c>Failures.Map</c>, which returns a
/// concrete <see cref="IErrorResponse"/>.
/// </summary>
public interface IErrorResponseFactory
{
    /// <summary>
    /// Builds the error envelope for a failure's HTTP <paramref name="details"/> (intent).
    /// The returned envelope is later augmented with per-request properties
    /// (traceId, path, custom) at execution time.
    /// </summary>
    IErrorResponse Create(FailureDetails details);
}
