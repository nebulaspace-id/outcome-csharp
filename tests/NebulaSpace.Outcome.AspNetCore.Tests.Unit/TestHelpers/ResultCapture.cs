namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit.TestHelpers;

internal sealed class ResultCapture
{
    public int StatusCode { get; init; }
    public string Body { get; init; } = "";
    public string Location { get; init; } = "";
}