using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// Implemented by failures that know their HTTP intent. Consulted by
/// <see cref="IHttpResultFactory.FromFailure"/> to obtain the failure's
/// <see cref="FailureDetails"/> (status code, message, code, errors), which is then
/// turned into a concrete envelope by the configured
/// <see cref="IErrorResponseFactory"/>. This keeps the factory Open/Closed — new
/// failure types add HTTP behavior without editing the mapping code, and the response
/// shape stays pluggable.
/// </summary>
public interface IHttpFailure
{
    /// <summary>Describes this failure's HTTP intent (status code + fields), independent of response shape.</summary>
    FailureDetails ToDetails();
}
