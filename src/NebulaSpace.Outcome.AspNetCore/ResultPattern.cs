using NebulaSpace.Outcome.AspNetCore.Http;
using NebulaSpace.Outcome.AspNetCore.Options;

namespace NebulaSpace.Outcome.AspNetCore;

/// <summary>
/// Static holder so the static <see cref="HttpResult"/> helpers can honor the
/// options registered once at startup via <c>AddResultPattern</c>.
/// </summary>
internal static class ResultPattern
{
    internal static ResultPatternOptions Options { get; set; } = new();
}
