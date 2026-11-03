namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// The default machine-readable <see cref="Code"/> and human-readable
/// <see cref="Message"/> used when a response is produced by the framework rather than by
/// application code — for example a 404 from routing, a 405 from method matching, or a
/// 401 from the authentication middleware.
///
/// <para>
/// Override a single entry with
/// <see cref="Options.ErrorOptions.MapStatusCode"/>. The registered value is only used by
/// <c>UseOutcomeStatusCodePages()</c>; failures created in application code keep their own
/// message and code.
/// </para>
/// </summary>
/// <param name="StatusCode">The HTTP status code this description applies to.</param>
/// <param name="Code">Machine-readable code, e.g. <c>"not_found"</c>.</param>
/// <param name="Message">Human-readable message rendered into the error envelope.</param>
public sealed record StatusCodeDescription(int StatusCode, string Code, string Message);
