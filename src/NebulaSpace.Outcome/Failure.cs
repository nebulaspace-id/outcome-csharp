namespace NebulaSpace.Outcome;

public abstract class Failure
{
    public string Message { get; }
    public string? Code { get; }

    /// <summary>Creates a failure without a machine-readable code (omit when not needed).</summary>
    protected Failure(string message)
        : this(message, null)
    {
    }

    protected Failure(string message, string? code)
    {
        Message = message;
        Code = code;
    }
}
