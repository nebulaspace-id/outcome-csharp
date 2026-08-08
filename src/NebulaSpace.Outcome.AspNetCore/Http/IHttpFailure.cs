using NebulaSpace.Outcome;
using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Http;

/// <summary>
/// Implemented by failures that know their HTTP intent. Consulted by
/// <see cref="IHttpResultFactory.FromFailure"/> to obtain the failure's
/// <see cref="FailureDetails"/> (status code, message, code, errors), which the
/// configured error envelope (<c>options.Error.SetResponse(typeof(MyError))</c>) renders
/// into a concrete body.
///
/// <para>
/// This keeps the factory <b>Open/Closed</b> — new failure types add HTTP behavior
/// without editing the mapping code, and the response shape stays typed. Combine with
/// <see cref="FailureMappingOptions"/> to override built‑in failures at startup.
/// </para>
/// </summary>
public interface IHttpFailure
{
    /// <summary>
    /// Describes this failure's HTTP intent (status code + fields), independent of the
    /// response shape. The configured error envelope turns this into a concrete body.
    /// </summary>
    FailureDetails ToDetails();
}
