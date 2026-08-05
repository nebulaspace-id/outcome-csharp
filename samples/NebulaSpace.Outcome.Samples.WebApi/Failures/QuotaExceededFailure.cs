using NebulaSpace.Outcome;

namespace NebulaSpace.Outcome.Samples.WebApi.Failures;

/// <summary>
/// Example of an application-defined failure, mapped to an HTTP response
/// via <c>ResultPatternOptions.MapFailure</c> in Program.cs.
/// </summary>
public sealed class QuotaExceededFailure : Failure
{
    public QuotaExceededFailure(string message) => Message = message;
    public string Message { get; }
}
