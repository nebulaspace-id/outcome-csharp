namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Example of an application-defined failure, mapped to an HTTP response
/// via <c>options.Error.MapFailure</c> in Program.cs.
/// </summary>
public sealed class QuotaExceededFailure(string message) : Failure(message, "quota_exceeded");
