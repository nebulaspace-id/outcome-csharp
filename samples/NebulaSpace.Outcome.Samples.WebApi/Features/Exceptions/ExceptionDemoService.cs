using NebulaSpace.Outcome.Samples.WebApi.Failures;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Exceptions;

/// <summary>
/// Service that demonstrates exception mapping: it THROWS <see cref="DomainException"/>
/// for a known business rule violation. Unlike expected failures (which return an
/// <c>Outcome</c>), this is a real exception that escapes the service and is caught by
/// the exception-handling middleware, which maps it via <c>options.Error.MapException</c>.
/// </summary>
public sealed class ExceptionDemoService : IExceptionDemoService
{
    /// <summary>
    /// Rejects a reserved keyword by throwing a <see cref="DomainException"/>. The error
    /// code travels with the instance, so the mapper reads it off <c>ex.Code</c>.
    /// </summary>
    public string Reserve(string keyword)
    {
        if (keyword.Equals("reserved", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                $"Keyword '{keyword}' sudah dipakai orang lain.");
        }

        return $"Keyword '{keyword}' berhasil direservasi.";
    }
}
