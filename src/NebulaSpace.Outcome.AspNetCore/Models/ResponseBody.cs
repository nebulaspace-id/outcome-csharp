namespace NebulaSpace.Outcome.AspNetCore.Models;

/// <summary>
/// Common envelope fields shared by success and error responses.
/// </summary>
public abstract class ResponseBody
{
    public int StatusCode { get; set; }
}
