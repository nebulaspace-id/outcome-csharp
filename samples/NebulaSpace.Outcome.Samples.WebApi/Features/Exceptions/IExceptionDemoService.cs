namespace NebulaSpace.Outcome.Samples.WebApi.Features.Exceptions;

/// <summary>
/// Demonstrates exception mapping: <see cref="Reserve"/> throws a
/// <see cref="Failures.DomainException"/> for a known business rule violation.
/// </summary>
public interface IExceptionDemoService
{
    /// <summary>
    /// Reserves a keyword. Throws <see cref="Failures.DomainException"/> (code
    /// <c>keyword_taken</c>) when the keyword is already taken.
    /// </summary>
    string Reserve(string keyword);
}
