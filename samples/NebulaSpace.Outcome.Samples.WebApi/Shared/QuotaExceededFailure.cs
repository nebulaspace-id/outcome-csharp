namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Example of an application-defined failure, mapped to an HTTP response
/// via <c>options.Error.MapFailure</c> in Program.cs.
/// </summary>
public sealed class QuotaExceededFailure : Failure
{
    public QuotaExceededFailure(string message) : base(message, "quota_exceeded")
    {
    }
}
