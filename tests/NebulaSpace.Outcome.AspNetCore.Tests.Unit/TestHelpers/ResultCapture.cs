namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit.TestHelpers;

internal sealed class ResultCapture
{
    public int StatusCode { get; init; }
    public string Body { get; init; } = "";
    public string Location { get; init; } = "";

    /// <summary>All response headers, keyed case-insensitively.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}