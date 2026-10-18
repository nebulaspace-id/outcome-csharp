namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// Example of an application-defined exception, mapped to an HTTP response
/// via <c>options.Error.MapException</c> in Program.cs. Unlike expected failures
/// (values), this is a real exception thrown by service code. The machine-readable
/// error code (<c>"domain_error"</c>) is assigned in Program.cs by <c>MapException</c>.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
