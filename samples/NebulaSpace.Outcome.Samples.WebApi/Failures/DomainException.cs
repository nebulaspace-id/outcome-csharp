namespace NebulaSpace.Outcome.Samples.WebApi.Failures;

/// <summary>
/// Example of an application-defined exception, mapped to an HTTP response
/// via <c>options.Error.MapException</c> in Program.cs. Unlike expected failures
/// (values), this is a real exception thrown by service code. The machine-readable
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
