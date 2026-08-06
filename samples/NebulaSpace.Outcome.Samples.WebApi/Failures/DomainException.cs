namespace NebulaSpace.Outcome.Samples.WebApi.Failures;

/// <summary>
/// Example of an application-defined exception, mapped to an HTTP response
/// via <c>options.Exceptions.Map</c> in Program.cs. Unlike expected failures
/// (values), this is a real exception thrown by service code. The machine-readable
/// <see cref="Code"/> is optional — supplied at throw time when a specific error code
/// is needed, <c>null</c> otherwise.
/// </summary>
public sealed class DomainException : Exception
{
    /// <summary>Machine-readable error code, if any. <c>null</c> when not supplied.</summary>
    public string? Code { get; }

    public DomainException(string message) : this(message, null)
    {
    }

    public DomainException(string message, string? code) : base(message)
    {
        Code = code;
    }
}
